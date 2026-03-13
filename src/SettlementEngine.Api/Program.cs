var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<SettlementEngine.Application.Commands.AddExpenseCommand>());

// TODO: Register infrastructure services (MongoDB, Redis, Kafka)
// builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// TODO: Map SignalR hub
// app.MapHub<BalanceHub>("/hubs/balance");

app.Run();
