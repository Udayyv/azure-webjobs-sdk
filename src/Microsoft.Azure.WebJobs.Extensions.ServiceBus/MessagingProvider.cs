﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// This class provides factory methods for the creation of instances
    /// used for ServiceBus message processing.
    /// </summary>
    public class MessagingProvider
    {
        private readonly ServiceBusOptions _options;
        private readonly ConcurrentDictionary<string, MessageReceiver> _messageReceiverCache = new ConcurrentDictionary<string, MessageReceiver>();
        private readonly ConcurrentDictionary<string, MessageSender> _messageSenderCache = new ConcurrentDictionary<string, MessageSender>();
        private readonly ConcurrentDictionary<string, ClientEntity> _clientEntityCache = new ConcurrentDictionary<string, ClientEntity>();

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="serviceBusOptions">The <see cref="ServiceBusOptions"/>.</param>
        public MessagingProvider(IOptions<ServiceBusOptions> serviceBusOptions)
        {
            _options = serviceBusOptions?.Value ?? throw new ArgumentNullException(nameof(serviceBusOptions));
        }

        /// <summary>
        /// Creates a <see cref="MessageProcessor"/> for the specified ServiceBus entity.
        /// </summary>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageProcessor"/> for.</param>
        /// <param name="connectionString">The ServiceBus connection string.</param>
        /// <returns>The <see cref="MessageProcessor"/>.</returns>
        public virtual MessageProcessor CreateMessageProcessor(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }

            return new MessageProcessor(GetOrAddMessageReceiver(entityPath, connectionString), _options.MessageHandlerOptions);
        }

        /// <summary>
        /// Creates a <see cref="MessageReceiver"/> for the specified ServiceBus entity.
        /// </summary>
        /// <remarks>
        /// You can override this method to customize the <see cref="MessageReceiver"/>.
        /// </remarks>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageReceiver"/> for.</param>
        /// <param name="connectionString">The ServiceBus connection string.</param>
        /// <returns></returns>
        public virtual MessageReceiver CreateMessageReceiver(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }

            return GetOrAddMessageReceiver(entityPath, connectionString);
        }

        /// <summary>
        /// Creates a <see cref="MessageSender"/> for the specified ServiceBus entity.
        /// </summary>
        /// <remarks>
        /// You can override this method to customize the <see cref="MessageSender"/>.
        /// </remarks>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="MessageSender"/> for.</param>
        /// <param name="connectionString">The ServiceBus connection string.</param>
        /// <returns></returns>
        public virtual MessageSender CreateMessageSender(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }

            return GetOrAddMessageSender(entityPath, connectionString);
        }

        /// <summary>
        /// Creates a <see cref="ClientEntity"/> for the specified ServiceBus entity.
        /// </summary>
        /// <remarks>
        /// You can override this method to customize the <see cref="ClientEntity"/>.
        /// </remarks>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="ClientEntity"/> for.</param>
        /// <param name="connectionString">The ServiceBus connection string.</param>
        /// <returns></returns>
        public virtual ClientEntity CreateClientEntity(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }

             return GetOrAddClientEntity(entityPath, connectionString);
        }

        /// <summary>
        /// Creates a <see cref="SessionMessageProcessor"/> for the specified ServiceBus entity.
        /// </summary>
        /// <param name="entityPath">The ServiceBus entity to create a <see cref="SessionMessageProcessor"/> for.</param>
        /// <param name="connectionString">The ServiceBus connection string.</param>
        /// <returns></returns>
        public virtual SessionMessageProcessor CreateSessionMessageProcessor(string entityPath, string connectionString)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityPath");
            }
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }

            return new SessionMessageProcessor(GetOrAddClientEntity(entityPath, connectionString), _options.SessionHandlerOptions);
        }

        public MessageReceiver GetOrAddMessageReceiver(string entityPath, string connectionString)
        {
            string cacheKey = $"{entityPath}-{connectionString}";
            return _messageReceiverCache.GetOrAdd(cacheKey,
                new MessageReceiver(connectionString, entityPath)
                {
                    PrefetchCount = _options.PrefetchCount
                });
        }

        private MessageSender GetOrAddMessageSender(string entityPath, string connectionString)
        {
            string cacheKey = $"{entityPath}-{connectionString}";
            return _messageSenderCache.GetOrAdd(cacheKey, new MessageSender(connectionString, entityPath));
        }

        private ClientEntity GetOrAddClientEntity(string entityPath, string connectionString)
        {
            string cacheKey = $"{entityPath}-{connectionString}";
            string[] arr = entityPath.Split(new string[] { "/Subscriptions/" }, StringSplitOptions.None);
            if (arr.Length == 2)
            {
                // entityPath for a subscription is "{TopicName}/Subscriptions/{SubscriptionName}"
                return _clientEntityCache.GetOrAdd(cacheKey, new SubscriptionClient(connectionString, arr[0], arr[1])
                {
                    PrefetchCount = _options.PrefetchCount
                });
            }
            else
            {
                // entityPath for a queue is "  {QueueName}"
                return _clientEntityCache.GetOrAdd(cacheKey, new QueueClient(connectionString, entityPath)
                {
                    PrefetchCount = _options.PrefetchCount
                });
            }
        }
    }
}
