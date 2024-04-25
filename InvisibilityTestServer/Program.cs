using Hangfire;
using Hangfire.Redis.StackExchange;
using Hangfire.SqlServer;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddHangfire(cfg =>
        {
            // cfg.UseSqlServerStorage("Server=(local);Database=hangfire;Trusted_Connection=True;",
            //     new SqlServerStorageOptions()
            //     {
            //         SlidingInvisibilityTimeout = TimeSpan.FromSeconds(1)
            //     });
            cfg.UseRedisStorage("localhost", new RedisStorageOptions()
            {
                Db = 1,
                InvisibilityTimeout = TimeSpan.FromSeconds(1),
                UseTransactions = false
            });
        })
    .AddHangfireServer(options => options.WorkerCount = 2);

var app = builder.Build();
app.UseHangfireDashboard("");

app.Run();
