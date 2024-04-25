// Copyright © 2013-2015 Sergey Odinokov, Marco Casamento
// This software is based on https://github.com/HangfireIO/Hangfire.Redis

// Hangfire.Redis.StackExchange is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as
// published by the Free Software Foundation, either version 3
// of the License, or any later version.
//
// Hangfire.Redis.StackExchange is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with Hangfire.Redis.StackExchange. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.Logging;
using Hangfire.Storage;
using StackExchange.Redis;

namespace Hangfire.Redis.StackExchange
{
    internal class RedisFetchedJob : IFetchedJob
    {
        private static readonly ILog logger = LogProvider.For<RedisFetchedJob>();
        
        private readonly RedisStorage _storage;
		private readonly IDatabase _redis;
        private bool _disposed;
        private bool _removedFromQueue;
        private bool _requeued;

        public RedisFetchedJob(
            [NotNull] RedisStorage storage, 
            [NotNull] IDatabase redis,
            [NotNull] string jobId, 
            [NotNull] string queue,
            [NotNull] DateTime fetchedAt)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            JobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
            Queue = queue ?? throw new ArgumentNullException(nameof(queue));
            FetchedAt = fetchedAt;
        }

        public string JobId { get; }
        public string Queue { get; }
        public DateTime FetchedAt { get; }

        private DateTime? GetFetchedValue()
        {
            var stackTrace = new StackTrace();
            logger.DebugFormat("GetFetchedValue: {0}, FetchedAt: {1}", JobId, FetchedAt);
            logger.Debug(stackTrace.ToString());
            return JobHelper.DeserializeDateTime(_redis.HashGet(_storage.GetRedisKey($"job:{JobId}"), "Fetched"));
        }

        public void RemoveFromQueue()
        {
            var fetchedAt = GetFetchedValue();
            if (_storage.UseTransactions)
            {
                var transaction = _redis.CreateTransaction();
                logger.DebugFormat("Current fetched value: {0}, ours: {1}, times equal: {2}", fetchedAt, FetchedAt, fetchedAt == FetchedAt);
                if (fetchedAt == FetchedAt)
                {
                    logger.DebugFormat("Removing {0} from dequeued", JobId);
                    transaction.ListRemoveAsync(_storage.GetRedisKey($"queue:{Queue}:dequeued"), JobId, -1);
                    logger.DebugFormat("Removed {0} from dequeued", JobId);
                    transaction.HashDeleteAsync(_storage.GetRedisKey($"job:{JobId}"), new RedisValue[] { "Fetched", "Checked" });
                }

                transaction.Execute();                
            } else
            {
                logger.DebugFormat("Current fetched value: {0}, ours: {1}, times equal: {2}", fetchedAt, FetchedAt, fetchedAt == FetchedAt);
                if (fetchedAt == FetchedAt)
                {
                    logger.DebugFormat("Removing {0} from dequeued", JobId);
                    _redis.ListRemoveAsync(_storage.GetRedisKey($"queue:{Queue}:dequeued"), JobId, -1);
                    logger.DebugFormat("Removed {0} from dequeued", JobId);
                    _redis.HashDeleteAsync(_storage.GetRedisKey($"job:{JobId}"), new RedisValue[] { "Fetched", "Checked" });
                }
            }
            _removedFromQueue = true;
        }

        public void Requeue()
        {
            var fetchedAt = GetFetchedValue();
            if (_storage.UseTransactions)
            {
                var transaction = _redis.CreateTransaction();
                transaction.ListRightPushAsync(_storage.GetRedisKey($"queue:{Queue}"), JobId);
                logger.DebugFormat("Current fetched value: {0}, ours: {1}, times equal: {2}", fetchedAt, FetchedAt, fetchedAt == FetchedAt);
                if (fetchedAt == FetchedAt)
                {
                    logger.DebugFormat("Removing {0} from dequeued", JobId);
                    transaction.ListRemoveAsync(_storage.GetRedisKey($"queue:{Queue}:dequeued"), JobId, -1);
                    logger.DebugFormat("Removed {0} from dequeued", JobId);
                    transaction.HashDeleteAsync(_storage.GetRedisKey($"job:{JobId}"), new RedisValue[] { "Fetched", "Checked" });
                }

                _redis.PublishAsync(_storage.SubscriptionChannel, JobId);
                transaction.Execute();
            } else
            {
                _redis.ListRightPush(_storage.GetRedisKey($"queue:{Queue}"), JobId);
                logger.DebugFormat("Current fetched value: {0}, ours: {1}, times equal: {2}", fetchedAt, FetchedAt, fetchedAt == FetchedAt);
                if (fetchedAt == FetchedAt)
                {
                    logger.DebugFormat("Removing {0} from dequeued", JobId);
                    _redis.ListRemoveAsync(_storage.GetRedisKey($"queue:{Queue}:dequeued"), JobId, -1);
                    logger.DebugFormat("Removed {0} from dequeued", JobId);
                    _redis.HashDeleteAsync(_storage.GetRedisKey($"job:{JobId}"), new RedisValue[] { "Fetched", "Checked" });
                }

                _redis.Publish(_storage.SubscriptionChannel, JobId);
            }
            _requeued = true;
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (!_removedFromQueue && !_requeued)
            {
                Requeue();
            }

            _disposed = true;
        }
    }
}
