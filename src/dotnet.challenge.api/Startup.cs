using System.Text.Json.Serialization;
using dotnet.challenge.api.Background;
using dotnet.challenge.api.Services;
using dotnet.challenge.data.Cache;
using dotnet.challenge.data.Entities;
using Microsoft.OpenApi.Models;

namespace dotnet.challenge.api;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory Reservations API", Version = "v1" });
        });

        services.AddSingleton<ISimpleObjectCache<Guid, Product>, SimpleObjectCache<Guid, Product>>();
        services.AddSingleton<ISimpleObjectCache<Guid, Reservation>, SimpleObjectCache<Guid, Reservation>>();
        services.AddSingleton<IInventoryService, InventoryService>();

        services.AddHostedService<ReservationExpirySweeper>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory Reservations API V1");
            c.RoutePrefix = "swagger";
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
