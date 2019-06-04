﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.ServiceBus.Bindings;
using Microsoft.Azure.WebJobs.ServiceBus.Listeners;

namespace Microsoft.Azure.WebJobs.ServiceBus.Triggers
{
    internal class ServiceBusTriggerBinding<T> : ITriggerBinding
    {
        private readonly string _parameterName;
        private readonly IObjectToTypeConverter<T> _converter;
        private readonly ITriggerDataArgumentBinding<T> _argumentBinding;
        private readonly IReadOnlyDictionary<string, Type> _bindingDataContract;
        private readonly ServiceBusAccount _account;
        private readonly ServiceBusOptions _options;
        private ServiceBusListener _listener;
        private readonly MessagingProvider _messagingProvider;


        public ServiceBusTriggerBinding(string parameterName, Type parameterType, ITriggerDataArgumentBinding<T> argumentBinding,
            ServiceBusAccount account, ServiceBusOptions options, MessagingProvider messagingProvider)
        {
            _parameterName = parameterName;
            _converter = typeof(T).IsArray ? CreateConverterArray(parameterType) as IObjectToTypeConverter<T> : CreateConverter(parameterType) as IObjectToTypeConverter<T>;
            _argumentBinding = argumentBinding;
            _bindingDataContract = CreateBindingDataContract(argumentBinding.BindingDataContract);
            _account = account;
            _options = options;
            _messagingProvider = messagingProvider;
        }

        public Type TriggerValueType
        {
            get
            {
                return typeof(T);
            }
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _bindingDataContract; }
        }

        public async Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            ITriggerData triggerData = null;
            IReadOnlyDictionary<string, object> bindingData;
            if (value != null)
            {
                T outMeesage = default(T);
                if (!_converter.TryConvert(value, out outMeesage))
                {
                    throw new InvalidOperationException("Unable to convert trigger to Message.");
                }
                triggerData = await (_argumentBinding as ITriggerDataArgumentBinding<T>).BindAsync(outMeesage, context);
                bindingData = CreateBindingData(outMeesage as Message, _listener?.Receiver, _listener?.MessageSession, triggerData.BindingData);
            }
            else
            {
                throw new InvalidOperationException("Unable to convert trigger to Message.");
            }

            return new TriggerData(triggerData.ValueProvider, bindingData);
        }

        public async Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            IListenerFactory factory = new ServiceBusListenerFactory(_account, context.Executor, _options, _messagingProvider);

            _listener = (ServiceBusListener)await factory.CreateAsync(context.CancellationToken);

            return _listener;
        }

        internal static IReadOnlyDictionary<string, Type> CreateBindingDataContract(IReadOnlyDictionary<string, Type> argumentBindingContract)
        {
            var contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("DeliveryCount", typeof(int));
            contract.Add("DeadLetterSource", typeof(string));
            contract.Add("LockToken", typeof(string));
            contract.Add("ExpiresAtUtc", typeof(DateTime));
            contract.Add("EnqueuedTimeUtc", typeof(DateTime));
            contract.Add("MessageId", typeof(string));
            contract.Add("ContentType", typeof(string));
            contract.Add("ReplyTo", typeof(string));
            contract.Add("SequenceNumber", typeof(long));
            contract.Add("To", typeof(string));
            contract.Add("Label", typeof(string));
            contract.Add("CorrelationId", typeof(string));
            contract.Add("UserProperties", typeof(IDictionary<string, object>));
            contract.Add("MessageReceiver", typeof(MessageReceiver));
            contract.Add("MessageSession", typeof(IMessageSession));

            if (argumentBindingContract != null)
            {
                foreach (KeyValuePair<string, Type> item in argumentBindingContract)
                {
                    // In case of conflict, binding data from the value type overrides the built-in binding data above.
                    contract[item.Key] = item.Value;
                }
            }

            return contract;
        }

        internal static IReadOnlyDictionary<string, object> CreateBindingData(Message value, MessageReceiver receiver, IMessageSession messageSession, IReadOnlyDictionary<string, object> bindingDataFromValueType)
        {
            var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (value != null)
            {
                SafeAddValue(() => bindingData.Add(nameof(value.SystemProperties.DeliveryCount), value.SystemProperties.DeliveryCount));
                SafeAddValue(() => bindingData.Add(nameof(value.SystemProperties.DeadLetterSource), value.SystemProperties.DeadLetterSource));
                SafeAddValue(() => bindingData.Add(nameof(value.SystemProperties.LockToken), value.SystemProperties.IsLockTokenSet ? value.SystemProperties.LockToken : string.Empty));
                SafeAddValue(() => bindingData.Add(nameof(value.ExpiresAtUtc), value.ExpiresAtUtc));
                SafeAddValue(() => bindingData.Add(nameof(value.SystemProperties.EnqueuedTimeUtc), value.SystemProperties.EnqueuedTimeUtc));
                SafeAddValue(() => bindingData.Add(nameof(value.MessageId), value.MessageId));
                SafeAddValue(() => bindingData.Add(nameof(value.ContentType), value.ContentType));
                SafeAddValue(() => bindingData.Add(nameof(value.ReplyTo), value.ReplyTo));
                SafeAddValue(() => bindingData.Add(nameof(value.SystemProperties.SequenceNumber), value.SystemProperties.SequenceNumber));
                SafeAddValue(() => bindingData.Add(nameof(value.To), value.To));
                SafeAddValue(() => bindingData.Add(nameof(value.Label), value.Label));
                SafeAddValue(() => bindingData.Add(nameof(value.CorrelationId), value.CorrelationId));
                SafeAddValue(() => bindingData.Add(nameof(value.UserProperties), value.UserProperties));
            }
            SafeAddValue(() => bindingData.Add("MessageReceiver", receiver));
            SafeAddValue(() => bindingData.Add("MessageSession", messageSession));

            if (bindingDataFromValueType != null)
            {
                foreach (KeyValuePair<string, object> item in bindingDataFromValueType)
                {
                    // In case of conflict, binding data from the value type overrides the built-in binding data above.
                    bindingData[item.Key] = item.Value;
                }
            }

            return bindingData;
        }

        private static void SafeAddValue(Action addValue)
        {
            try
            {
                addValue();
            }
            catch
            {
                // some message property getters can throw, based on the
                // state of the message
            }
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ServiceBusTriggerParameterDescriptor
            {
                Name = _parameterName,
                EntityPath = _account.EntityPath,
                DisplayHints = ServiceBusBinding.CreateParameterDisplayHints(_account.EntityPath, true)
            };
        }

        private static IObjectToTypeConverter<Message> CreateConverter(Type parameterType)
        {
            return new CompositeObjectToTypeConverter<Message>(
                    new OutputConverter<Message, Message>(new IdentityConverter<Message>()),
                    new OutputConverter<string, Message>(StringTodMessageConverterFactory.Create(parameterType)));
        }

        private static IObjectToTypeConverter<Message[]> CreateConverterArray(Type parameterType)
        {
            return new CompositeObjectToTypeConverter<Message[]>(
                    new OutputConverter<Message[], Message[]>(new IdentityConverter<Message[]>()),
                    new OutputConverter<string[], Message[]>(StringTodMessageConverterFactory.CreateAsMultiple(parameterType)));
        }
    }
}
