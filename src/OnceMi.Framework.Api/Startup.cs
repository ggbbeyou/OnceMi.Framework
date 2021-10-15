using IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
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
using OnceMi.Framework.Extension.Authorizations;
using OnceMi.Framework.Extension.DependencyInjection;
using OnceMi.Framework.Extension.Filters;
using OnceMi.Framework.Extension.Helpers;
using OnceMi.Framework.Util.Json;
using OnceMi.Framework.Extension.Middlewares;
using System;
using System.Text;
using System.Text.Encodings.Web;

namespace OnceMi.Framework.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //��Ӷ���ı��룬����
            //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            //ConfigManager
            services.AddConfig();

            #region IdGenerator

            services.AddIdGenerator(x =>
            {
                x.AppId = Configuration.GetValue<ushort>("AppSettings:AppId");
            });

            #endregion

            //MemoryCache
            services.AddMemoryCache();
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
                || string.IsNullOrWhiteSpace(config.SecretKey))
                {
                    throw new Exception("Configuration can not bind oss config.");
                }
                option.Provider = OSSProvider.Minio;
                option.Endpoint = config.Endpoint;
                option.Region = config.Region;
                option.AccessKey = config.AccessKey;
                option.SecretKey = config.SecretKey;
                option.IsEnableCache = config.IsEnableCache;
                option.IsEnableHttps = config.IsEnableHttps;
            });

            #endregion

            #region Service & Repository

            services.AddRepository();
            services.AddService();

            #endregion

            #region ����

            services.AddCors(options =>
            {
                options.AddPolicy(ConfigConstant.DefaultOriginsName, policy =>
                 {
                     policy.AllowAnyHeader()
                     .AllowAnyMethod()
                     .AllowAnyOrigin();
                 });
            });

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

            var tokenConfig = Configuration.GetSection("TokenManagement").Get<TokenManagementNode>();
            var identityServerConfig = Configuration.GetSection("IdentityServer").Get<IdentityServerNode>();

            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(x =>
                {
                    if (identityServerConfig.IsEnabledIdentityServer)
                    {
                        #region IdentityServer

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

                        #endregion
                    }
                    else
                    {
                        #region ������֤

                        x.RequireHttpsMetadata = false;
                        x.SaveToken = true;
                        x.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenConfig.Secret)),
                            ValidIssuer = tokenConfig.Issuer,
                            ValidAudience = tokenConfig.Audience,
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            RoleClaimType = JwtClaimTypes.Role,
                            NameClaimType = JwtClaimTypes.Name,
                            RequireExpirationTime = true, //����ʱ��
                            ClockSkew = TimeSpan.FromMinutes(5),
                        };

                        #endregion
                    }
                    x.Events = new JwtBearerEvents
                    {
                        OnChallenge = async context =>
                        {
                            await CustumJwtBearerEvents.OnChallenge(context);
                        },
                        OnForbidden = async context =>
                        {
                            await CustumJwtBearerEvents.OnForbidden(context);
                        },
                        OnAuthenticationFailed = async context =>
                        {
                            await CustumJwtBearerEvents.OnAuthenticationFailed(context);
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

            #region  �Զ�ע��

            services.AddAutoInjection();

            #endregion

            #region HealthCheck

            services.AddHealthCheckService();

            #endregion

            #region ������־

            //��.net6�н������ô�api
            //services.AddHttpLogging(logging =>
            //{
            //    // Customize HTTP logging here.
            //    logging.LoggingFields = HttpLoggingFields.All;
            //    logging.RequestBodyLogLimit = 4096;
            //    logging.ResponseBodyLogLimit = 4096;
            //});

            #endregion

            #region Controller

            services.AddHttpContextAccessor();
            //������ֹͬ�� I/O �Ĳ������ܻᵼ���̳߳���Դ���㣬��������Ӧ������Ӧ�� ����ʹ�ò�֧���첽 I/O �Ŀ�ʱ�������� AllowSynchronousIO
            //services.Configure<KestrelServerOptions>(x => x.AllowSynchronousIO = true);
            //services.Configure<IISServerOptions>(x => x.AllowSynchronousIO = true);
            services.AddHostedService<LifetimeEventsService>();
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
                    options.JsonSerializerOptions.Converters.Add(new ExceptionConverter());
                    options.JsonSerializerOptions.Converters.Add(new TypeConverter());
                    //С�շ�
                    options.JsonSerializerOptions.PropertyNamingPolicy = ConfigConstant.DefaultJsonNamingPolicy;
                    //ѭ������
                    //options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
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
            , ILoggerFactory loggerFactory
            , ConfigManager config)
        {
            #region ȫ���쳣����

            //�˴�ע�����ȫ���쳣�������ڲ���Filter���޷�������쳣���ڲ��쳣��
            //������Ҫ�쳣������Ӧ��ʵ���޸�Filter���쳣������Filter���쳣�����У������ٴν���˴�����
            app.UseExceptionHandler(builder =>
            {
                builder.Run(async context =>
                {
                    await RewriteHelper.GlobalExceptionHandler(context, loggerFactory, ConfigConstant.DefaultJsonNamingPolicy);
                });
            });

            #endregion

            #region ������־

            //������־������.net6�����ô�api
            //app.UseHttpLogging();
            //������־
            app.UseRequestLogging();

            #endregion

            //Swagger
            app.UseSwaggerWithUI();
            //��ʼ�����ݿ�
            app.UseDbSeed();
            //�������
            app.UseHealthChecks();
            //����
            app.UseCors(ConfigConstant.DefaultOriginsName);
            //��Ϣ����
            app.UseMessageQuene();

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                if (config.AppSettings.HealthCheck.IsEnabledHealthCheckUI)
                {
                    //MapHealthChecksUIӦ��ͳһд��UseHealthChecks��
                    //������bug�������뿴UseHealthChecks��ע��
                    //issue��https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/issues/716
                    endpoints.MapHealthChecksUI(options =>
                    {
                        options.UseRelativeResourcesPath = false;
                        options.UseRelativeApiPath = false;
                        options.UseRelativeWebhookPath = false;
                        options.UIPath = config.AppSettings.HealthCheck.HealthCheckUIPath;
                    }).AllowAnonymous();
                }

                endpoints.MapControllers();
            });
        }
    }
}
