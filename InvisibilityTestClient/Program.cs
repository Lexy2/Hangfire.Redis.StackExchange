// See https://aka.ms/new-console-template for more information

using Hangfire;
using Hangfire.Redis.StackExchange;
using InvisibilityTestClient;

GlobalConfiguration.Configuration.UseRedisStorage("localhost", new RedisStorageOptions()
{
    Db = 1,
});


// GlobalConfiguration.Configuration.UseSqlServerStorage("Server=(local);Database=hangfire;Trusted_Connection=True;");

BackgroundJob.Enqueue<Test>(t => t.LongTask(TimeSpan.FromSeconds(600), CancellationToken.None));
