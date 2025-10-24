using AudioRecognitionApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews().AddNewtonsoftJson();

builder.Services.AddScoped<IAudioRecognitionService, AudioRecognitionService>();
builder.Services.AddScoped<ILyricsService, LyricsService>();

builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Audio}/{action=Index}/{id?}");

app.Run();
