using System.Text;
using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 数据库与业务服务
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<MasterService>();
builder.Services.AddScoped<ShopService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<SupplementService>();
builder.Services.AddScoped<PromotionService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ImportExportService>();

// JWT 认证
var jwt = builder.Configuration.GetSection("Jwt");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = key
        };
    });
builder.Services.AddAuthorization();

// 控制器与 JSON
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new FlexibleDateTimeConverter());
    o.JsonSerializerOptions.Converters.Add(new FlexibleNullableDateTimeConverter());
});
builder.Services.AddHttpContextAccessor();

// 附件上传大小限制（多张图片）
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 60 * 1024 * 1024);

// 跨域（开发期前端 Vite 5173，生产由 nginx 同源代理，这里放开以便灵活部署）
builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 首次启动：自动建库建表 + 写入默认用户
await DatabaseInitializer.EnsureAsync(app);
await UserSeed.EnsureAsync(app);

Console.WriteLine($"[EcomSales.Api] 启动完成，环境：{app.Environment.EnvironmentName}");
app.Run();
