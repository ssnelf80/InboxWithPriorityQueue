using InboxPriorityQueue;
using InboxPriorityQueue.Context;
using InboxPriorityQueue.InboxPoll;
using InboxPriorityQueue.Manager;
using InboxPriorityQueue.Processors;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ILoggerFactory, LoggerFactory>();
builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
builder.Services.AddTransient<IInboxProcessor, EmptyInboxProcessor>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<InboxPollConfiguration>(builder.Configuration.GetSection("InboxPollConfiguration"));

builder.Services.AddSingleton<InboxContext>();
builder.Services.AddTransient<InboxWorker>();
builder.Services.AddHostedService<InboxPollService>();

builder.Services.ConfigureFluentMigrator();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MigrateInboxDatabase();

app.MapGet("/testApi", async () =>
    {
        using var scope1 = app.Services.CreateScope();
        var manager1 = scope1.ServiceProvider.GetRequiredService<InboxWorker>();
        for (var i = 0; i < 10; i++)
        {
            var values = new string[100_000];
            for (var j = 0; j < 15_000; j++)
            {
                values[j] = Guid.NewGuid().ToString("N");
            }
            
            await manager1.AddOrUpdateInboxItemsAsync(values);
        }
        
        return await manager1.IsEmptyQueueAsync(default);
    })
    .WithName("testApi")
    .WithOpenApi();

app.Run();


