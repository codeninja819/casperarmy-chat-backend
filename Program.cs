using CasperArmy_Chat.Data;
using CasperArmy_Chat.Hubs;
using CasperArmy_Chat.Services;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging.Signing;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Buffers.Text;
using Org.BouncyCastle.Utilities.Encoders;
using Org.BouncyCastle.Utilities.IO.Pem;
using static System.Net.Mime.MediaTypeNames;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Signers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
{
  var services = builder.Services;
  var env = builder.Environment;

  // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
  services.AddEndpointsApiExplorer();
  services.AddSwaggerGen();
  services.AddCors(options =>
  {
    options.AddPolicy("CorsPolicy",
    builder =>
    {
      builder.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed((x) => true).AllowCredentials();
    });
  });
  services.AddSignalR();

  services.AddDbContext<DataContext>(options =>
     options.UseNpgsql(builder.Configuration.GetConnectionString("psqlCasperArmyServer")));

  services.AddControllers().AddJsonOptions(x =>
  {
    // serialize enums as strings in api responses (e.g. Role)
    x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

    // ignore omitted parameters on models to enable optional params (e.g. User update)
    x.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
  });
  // services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

  // configure DI for application services
  services.AddScoped<IDataService, DataService>();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
  var services = scope.ServiceProvider;
  try
  {
    var context = services.GetRequiredService<DataContext>();
    DbInitializer.Initialize(context);
  }
  catch (Exception ex)
  {
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred creating the DB.");
  }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

{
  app.UseCors("CorsPolicy");

  app.UseHttpsRedirection();

  app.UseAuthorization();

  app.MapControllers();
  app.MapHub<ChatHub>("/Chat");
}

app.Run();
