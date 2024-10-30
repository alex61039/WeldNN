using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BusinessLayer.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.SwaggerGen;
using WebAPI.Configuration;
using WebAPI.Security.Tokens;
using WebAPI.Services;

namespace WebAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
        {
            readonly IApiVersionDescriptionProvider provider;

            public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) =>
              this.provider = provider;

            public void Configure(SwaggerGenOptions options)
            {
                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerDoc(
                        description.GroupName,
                        new Swashbuckle.AspNetCore.Swagger.Info()
                        {
                            Title = $"Welding API {description.ApiVersion}",
                            Version = description.ApiVersion.ToString(),
                        }
                        );

                    // options.CustomOperationIds(e => $"{e.ActionDescriptor.RouteValues["controller"]}{e.HttpMethod}");
                    options.CustomOperationIds(e => $"{e.ActionDescriptor.RouteValues["controller"]}_{e.ActionDescriptor.RouteValues["action"]}");
                }
            }
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // App Settings
            services.Configure<StorageOptions>(Configuration.GetSection("Storage"));

            services.Configure<TokenOptions>(Configuration.GetSection("TokenOptions"));
            var tokenOptions = Configuration.GetSection("TokenOptions").Get<TokenOptions>();

            // Set JWT authentication
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwtBearerOptions =>
                {
                    jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = tokenOptions.Issuer,
                        ValidAudience = tokenOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(tokenOptions.SecretKey)),
                        ClockSkew = TimeSpan.Zero
                    };
                });


            // Dependency Injections
            services.AddSingleton<WebAPI.Security.Tokens.TokenHandler>();
            services.AddScoped<AuthenticationService>();

            // DB Context
            services.AddScoped<DataLayer.Welding.WeldingContext>(_ => new DataLayer.Welding.WeldingContext(Configuration.GetConnectionString("DefaultConnection"), false));

            // Welding/User services
            services.AddScoped<BusinessLayer.Services.Notifications.NotificationsService>();
            services.AddScoped<BusinessLayer.Services.QueueTasks.QueueTasksService>();
            services.AddScoped<BusinessLayer.Interfaces.Storage.IDocumentsService, BusinessLayer.Services.Storage.DocumentsService>();
            services.AddScoped<BusinessLayer.Welding.Machine.MachineStateService>();
            services.AddScoped<BusinessLayer.Welding.Controls.ProgramControlsService>();
            services.AddScoped<BusinessLayer.Services.Mailer.MailerService>();


            // Localization
            CultureInfo[] supportedCultures = new[]
                {
                new CultureInfo("en")
            };

            services.Configure<RequestLocalizationOptions>(options =>
            {
                options.DefaultRequestCulture = new RequestCulture("en");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
                options.SetDefaultCulture("en");
            });


            // CORs
            services.AddCors();

            // Disable Automatic Model State Validation
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });


            // MVC and serialization rules
            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddJsonOptions(opt =>
                {
                    // set PascalCase
                    opt.SerializerSettings.ContractResolver = new DefaultContractResolver();
                    opt.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                });


            // Swagger documentation
            /*
            services.AddSwaggerGen(swaggerOptions =>
            {
                string apiVersion = "v1";
                string apiTitle = "WeldingTelecom API";

                swaggerOptions.SwaggerDoc(string.Format("{0}", apiVersion), new Swashbuckle.AspNetCore.Swagger.Info
                {
                    Version = apiVersion,
                    Title = apiTitle
                });
                swaggerOptions.DescribeAllEnumsAsStrings();
                // swaggerOptions.OperationFilter<FormFileUploadOperationFilter>(Array.Empty<object>());
            });
            */

            // Versioning
            services.AddApiVersioning(options =>
            {
                options.AssumeDefaultVersionWhenUnspecified = true;
            });


            services.AddVersionedApiExplorer(options => options.GroupNameFormat = "'v'VVV");
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            services.AddSwaggerGen();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApiVersionDescriptionProvider provider)
        {
            app.UseCors(builder =>
                builder
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin()
                // .AllowCredentials()
                );


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            // app.UseHttpsRedirection();

            app.UseAuthentication();

            // Configure the localization options
            var supportedCultures = new[]
            {
            new CultureInfo("en")
        };

            app.UseRequestLocalization(
                new RequestLocalizationOptions
                {
                    DefaultRequestCulture = new RequestCulture("en"),
                    SupportedCultures = supportedCultures,
                    SupportedUICultures = supportedCultures,
                    FallBackToParentCultures = true,
                    FallBackToParentUICultures = true,
                    RequestCultureProviders = null
                });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "api_versioned",
                    template: "api/v{version:apiVersion}/{controller}/{action}");

                routes.MapRoute(
                    name: "api",
                    template: "api/{controller}/{action}");

                routes.MapRoute(
                    name: "home",
                    template: "{controller=Home}/{action=Index}");
            });


            /*
            app.UseSwagger(c =>
            {
                c.PreSerializeFilters.Add((swagger, httpReq) => swagger.Host = httpReq.Host.Value);
            });
            app.UseSwaggerUI(x =>
            {
                x.RoutePrefix = "swagger/ui";
                x.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
            });
            */

            app.UseSwagger();
            app.UseSwaggerUI(
                options =>
                {
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerEndpoint(
                            $"/swagger/{description.GroupName}/swagger.json",
                            description.GroupName.ToUpperInvariant());
                    }
                });

            // app.UseStaticFiles();
        }
    }
}
