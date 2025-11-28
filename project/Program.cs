using AudioRecognitionApp.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50 MB
});
builder.Services.AddControllersWithViews();

// AÑADIR ESTO:
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
});

// Add services to the container
builder.Services.AddControllersWithViews().AddNewtonsoftJson();

// Registrar servicios personalizados
builder.Services.AddScoped<IAudioRecognitionService, AudioRecognitionService>();
builder.Services.AddScoped<ILyricsService, LyricsService>();

// Agregar HttpClient factory
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// NO necesitamos Authorization para esta app
// app.UseAuthorization();  // ← Esta línea puede estar causando el problema

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Audio}/{action=Index}/{id?}");

app.Run();