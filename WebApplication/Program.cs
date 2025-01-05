using InboxPriorityQueue.Context;
using InboxPriorityQueue.InboxBatch;
using InboxPriorityQueue.InboxPoll;
using InboxPriorityQueue.Processors;
using InboxPriorityQueue.Worker;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ILoggerFactory, LoggerFactory>();
builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
builder.Services.AddTransient<IInboxProcessor, EmptyInboxProcessor>();
builder.Services.AddSingleton<InboxBatchWriter>();

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

app.MapGet("/testApi", () =>
    {
        using var scope1 = app.Services.CreateScope();
        var writer = scope1.ServiceProvider.GetRequiredService<InboxBatchWriter>();
        for (var i = 0; i < 250_000; i++)
        {
            writer.Enqueue(Guid.NewGuid().ToString("N"));
        }
        
        return true;
    })
    .WithName("testApi")
    .WithOpenApi();

app.Run();


