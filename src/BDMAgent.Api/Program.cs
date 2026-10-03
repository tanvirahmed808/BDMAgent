using BDMAgent.Application.Abstractions; using BDMAgent.Application.Services; using BDMAgent.Infrastructure.Data; using BDMAgent.Infrastructure.Services; using Microsoft.EntityFrameworkCore;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddControllers(); builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen(); builder.Services.AddScoped<LeadScoringService>();
builder.Services.AddDbContext<BdmDbContext>(o=>o.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
builder.Services.AddHttpClient<IAiService,OpenAiCompatibleAiService>(); builder.Services.AddScoped<IEmailSender,SmtpEmailSender>(); builder.Services.AddHostedService<FollowUpWorker>();
var app=builder.Build(); app.UseSwagger(); app.UseSwaggerUI(); app.UseDefaultFiles(); app.UseStaticFiles(); app.MapControllers(); app.MapGet("/health",()=>Results.Ok(new{status="ok",service="BDM Agent",utc=DateTime.UtcNow}));
using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<BdmDbContext>(); if(app.Configuration.GetValue("Database:AutoCreate",true)) await db.Database.EnsureCreatedAsync();}
app.Run();
