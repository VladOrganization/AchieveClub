using AchieveClub.Server.Auth;
using AchieveClub.Server.Services;
using AchieveClub.Server.SwaggerVersioning;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace AchieveClub.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            /*var otlpExporterConnectionString = builder.Configuration.GetConnectionString("DashboardConnection")
                ?? throw new Exception("Add 'DashboardConnection' to config");
            var otlpExporterEndpoint = new Uri(otlpExporterConnectionString);*/

            /*builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("AchieveClub"))
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();

                    metrics.AddOtlpExporter(configure => configure.Endpoint = otlpExporterEndpoint);
                })
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddEntityFrameworkCoreInstrumentation();

                    tracing.AddOtlpExporter(configure => configure.Endpoint = otlpExporterEndpoint);
                });
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.AddOtlpExporter(configure => configure.Endpoint = otlpExporterEndpoint);
            });*/

            builder.Services.AddCors();

            var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection")
                                        ?? throw new InvalidConfigurationException("Add 'RedisConnection' to config");
            builder.Services.AddOutputCache()
                .AddStackExchangeRedisOutputCache(options => options.Configuration = redisConnectionString);
            builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = redisConnectionString; });

            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<GzipCompressionProvider>();
            });

            builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.SmallestSize;
            });

            var jwtSettings = new JwtSettings();
            builder.Configuration.Bind("JwtSettings", jwtSettings);
            jwtSettings.Key = builder.Configuration["jwt_key"]
                              ?? throw new InvalidConfigurationException("Add 'jwt_key' to config");

            builder.Services.AddSingleton(jwtSettings);
            builder.Services.AddTransient<JwtTokenCreator>();
            builder.Services.AddTransient<HashService>();
            builder.Services.AddTransient<EmailProofService>();

            builder.Services.AddAuthentication(i =>
                {
                    i.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    i.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    i.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    i.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidAudience = jwtSettings.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                        ClockSkew = jwtSettings.Expire
                    };
                    options.SaveToken = true;
                    options.Events = new JwtBearerEvents();
                    options.Events.OnMessageReceived += context =>
                    {
                        if (context.Request.Cookies.ContainsKey("X-Access-Token"))
                        {
                            context.Token = context.Request.Cookies["X-Access-Token"];
                        }

                        return Task.CompletedTask;
                    };
                })
                .AddCookie(options =>
                {
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.IsEssential = true;
                });

            builder.Services.AddControllers();

            builder.Services.AddSignalR();

            builder.Services.AddApiVersioning(config =>
                {
                    config.DefaultApiVersion = new ApiVersion(1, 0);
                    config.AssumeDefaultVersionWhenUnspecified = true;
                    config.ReportApiVersions = true;
                })
                .AddMvc()
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

            builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            builder.Services.AddSwaggerGen(options =>
            {
                options.OperationFilter<SwaggerDefaultValues>();
                options.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme."
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "bearerAuth" }
                        },
                        new string[] { }
                    }
                });
            });

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                                   ?? throw new InvalidConfigurationException("Add 'DefaultConnection' to config");
            builder.Services.AddDbContext<ApplicationContext>(options => options.UseNpgsql(connectionString));

            if (builder.Environment.IsProduction())
            {
                builder.WebHost.ConfigureKestrel(kestrelOptions =>
                {
                    kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = X509Certificate2.CreateFromPemFile(
                            builder.Configuration["Certificates:Public"] ??
                            throw new Exception("Add 'Certificates:Public' to config"),
                            builder.Configuration["Certificates:Private"] ??
                            throw new Exception("Add 'Certificates:Public' to config"));
                    });
                });
            }

            var app = builder.Build();

            if (app.Environment.IsProduction())
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            app.UseCors(policyBuilder => policyBuilder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("*")
            );

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseResponseCompression();

            app.UseOutputCache();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(
                    options =>
                    {
                        foreach (var description in app.DescribeApiVersions())
                        {
                            options.SwaggerEndpoint(
                                $"/swagger/{description.GroupName}/swagger.json",
                                description.GroupName);
                        }
                    }
                );
            }

            app.MapControllers();

            app.MapHub<AchieveHub>("/achieve");

            app.Run();
        }
    }
}