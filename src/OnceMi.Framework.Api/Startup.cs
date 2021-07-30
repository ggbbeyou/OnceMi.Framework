using IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OnceMi.AspNetCore.IdGenerator;
using OnceMi.AspNetCore.MQ;
using OnceMi.AspNetCore.OSS;
using OnceMi.Framework.Api.Middlewares;
using OnceMi.Framework.Config;
using OnceMi.Framework.DependencyInjection;
using OnceMi.Framework.Extension.Authorizations;
using OnceMi.Framework.Extension.Filters;
using OnceMi.Framework.Extension.Helpers;
using OnceMi.Framework.Extension.Middlewares;
using OnceMi.Framework.Model;
using OnceMi.Framework.Util.Json;
using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace OnceMi.Framework.Api
{
    public class Startup
    {
        private const string _defaultOrigins = "DefaultCorsPolicy";
        //ȫ��json���󷵻����ã�Ĭ��С�շ�
        private JsonNamingPolicy _jsonNamingPolicy = JsonNamingPolicy.CamelCase;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            #region IdGenerator

            services.AddIdGenerator(x =>
            {
                x.AppId = Configuration.GetValue<ushort>("AppSettings:AppId");
            });

            #endregion

            //MemoryCache
            services.AddMemoryCache();
            //ConfigManager
            services.AddConfig();
            //Db
            services.AddDatabase();
            //AutoMapper
            services.AddMapper();
            //RedisCahe
            services.AddRedisCache();
            //Swagger
            services.AddSwagger();

            #region OSS

            services.AddOSSService(option =>
            {
                OSSConfigNode config = Configuration.GetSection("OSSProvider").Get<OSSConfigNode>();
                if (config == null
                || string.IsNullOrWhiteSpace(config.Endpoint)
                || string.IsNullOrWhiteSpace(config.AccessKey)
                || string.IsNullOrWhiteSpace(config.SecretKey)
                || string.IsNullOrWhiteSpace(config.DefaultBucketName))
                {
                    throw new Exception("Configuration can not bind oss config.");
                }
                option.Provider = OSSProvider.Minio;
                option.Endpoint = config.Endpoint;
                option.Region = config.Region;
                option.AccessKey = config.AccessKey;
                option.SecretKey = config.SecretKey;
                option.IsEnableCache = config.IsEnableCache;
            });

            #endregion

            #region Service & Repository

            services.AddRepository();
            services.AddService();

            #endregion

            #region ����

            services.AddCors(options =>
            {
                options.AddPolicy(_defaultOrigins, policy =>
                 {
                     policy.AllowAnyHeader()
                     .AllowAnyMethod()
                     .AllowAnyOrigin();
                 });
            });

            #endregion

            #region HealthCheck

            services.AddHealthCheckService();

            #endregion

            #region ��Ϣ����

            services.AddMessageQuene(option =>
            {
                option.UseExternalRedisClient = true;
                option.AppId = Configuration.GetValue<int>("AppSettings:AppId");
                option.ProviderType = Configuration.GetValue<MqProviderType>("MessageQueneSetting:ProviderType");
                option.Connectstring = Configuration.GetValue<string>("MessageQueneSetting:ConnectionString");
            });

            #endregion

            #region Aop

            services.AddAop();

            #endregion

            #region ��֤����Ȩ

            //Json���л�����
            services.Configure<CustumJsonSerializerOptions>(option =>
            {
                option.JsonNamingPolicy = _jsonNamingPolicy;
            });
            var token = Configuration.GetSection("TokenManagement").Get<TokenManagementNode>();

            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(x =>
                {
                    x.Authority = Configuration.GetValue<string>("IdentityServer:Url");
                    x.Audience = Configuration.GetValue<string>("IdentityServer:Audience");
                    x.RequireHttpsMetadata = Configuration.GetValue<bool>("IdentityServer:RequireHttps");
                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        //RoleClaimType = ClaimTypes.Role,
                        NameClaimType = JwtClaimTypes.Name,

                        RequireExpirationTime = true, //����ʱ��
                        ClockSkew = TimeSpan.FromMinutes(5),
                    };
                    x.Events = new JwtBearerEvents
                    {
                        OnChallenge = async context =>
                        {
                            if (!string.IsNullOrEmpty(context.ErrorDescription))
                                await WriteResponse(context.Response, StatusCodes.Status401Unauthorized, context.ErrorDescription);
                            else
                                await WriteResponse(context.Response, StatusCodes.Status401Unauthorized, "Unauthorized");
                            context.HandleResponse();
                        },
                        OnForbidden = async context =>
                        {
                            await WriteResponse(context.Response, StatusCodes.Status403Forbidden, "Forbidden");
                        },
                        OnAuthenticationFailed = context =>
                        {
                            // ������ڣ����<�Ƿ����>��ӵ�������ͷ��Ϣ��
                            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                            {
                                context.Response.Headers.Add("Token-Expired", "true");
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddAuthorization(options =>
            {
                //ȫ���û���Ȩ
                options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            #endregion

            #region Quartz��ʱ����

            services.AddQuartz();

            #endregion

            #region Controller

            services.AddHttpContextAccessor();
            services.Configure<KestrelServerOptions>(x => x.AllowSynchronousIO = true);
            services.Configure<IISServerOptions>(x => x.AllowSynchronousIO = true);
            services.AddHostedService<LifetimeEventsService>();
            services.Configure<HostOptions>(option =>
            {
                option.ShutdownTimeout = TimeSpan.FromSeconds(10);
            });

            services.AddControllers(options =>
            {
                //ȫ���쳣
                options.Filters.Add(typeof(GlobalExceptionFilter));
                //��װ�������ݸ�ʽ
                options.Filters.Add(typeof(GlobalApiResponseFilter));
                //ȫ����Ȩ������
                options.Filters.Add(typeof(GlobalPermissionFilter));
                //�ظ���������� δ���
                //options.Filters.Add(typeof(GolbalTranActionFilter));
            })
                .AddJsonOptions(options =>
                {
                    //���ĵ������ַ����л�
                    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                    //���������ֶ�
                    options.JsonSerializerOptions.IncludeFields = true;
                    //���Դ�Сд
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    //DateTime
                    options.JsonSerializerOptions.Converters.Add(new DateTimeConverter());
                    options.JsonSerializerOptions.Converters.Add(new DateTimeNullableConverter());
                    //С�շ�
                    options.JsonSerializerOptions.PropertyNamingPolicy = _jsonNamingPolicy;
                });

            //ApiBehaviorOptions������AddControllers֮��
            services.Configure<ApiBehaviorOptions>(options =>
            {
                //��дģ����֤�������ݸ�ʽ
                options.InvalidModelStateResponseFactory = RewriteHelper.RewriteInvalidModelStateResponse;
            });

            #endregion
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app
            , IWebHostEnvironment env
            , ILoggerFactory loggerFactory)
        {
            #region ȫ���쳣����

            //�˴�ע�����ȫ���쳣�������ڲ���Filter���޷�������쳣���ڲ��쳣��
            //������Ҫ�쳣������Ӧ��ʵ���޸�Filter���쳣������Filter���쳣�����У������ٴν���˴�����
            app.UseExceptionHandler(builder =>
            {
                builder.Run(async context =>
                {
                    await RewriteHelper.GlobalExceptionHandler(context, loggerFactory, _jsonNamingPolicy);
                });
            });

            #endregion

            //�����ļ�
            app.UseConfig();
            //Swagger
            app.UseSwaggerWithUI();
            //��ʼ�����ݿ�
            app.UseDbSeed();
            //�������
            app.UseHealthChecks();
            //����
            app.UseCors(_defaultOrigins);
            //RabbitMQ��Ϣ����
            app.UseMessageQuene();

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            //д������־
            app.UseRequestLogging();

            app.UseEndpoints(endpoints =>
            {
                //MapHealthChecksUIӦ��ͳһд��UseHealthChecks��
                //������bug�������뿴UseHealthChecks��ע��
                //issue��https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/issues/716
                endpoints.MapHealthChecksUI(options =>
                {
                    options.UseRelativeResourcesPath = false;
                    options.UseRelativeApiPath = false;
                    options.UseRelativeWebhookPath = false;
                    options.UIPath = "/sys/health-ui";
                }).AllowAnonymous();

                endpoints.MapControllers();
            });
        }

        private async Task WriteResponse(HttpResponse response, int statusCode, string message)
        {
            response.ContentType = "application/json";
            response.StatusCode = statusCode;

            await response.WriteAsync(JsonUtil.SerializeToString(new ResultObject<object>()
            {
                Code = statusCode,
                Message = message,
            }, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = _jsonNamingPolicy
            }));
        }
    }
}
