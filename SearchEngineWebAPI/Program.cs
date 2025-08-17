//
// FILE: Program.cs
//
// This file sets up the ASP.NET Core web application,
// configures services (like your search service), and
// defines the request pipeline.
//
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SearchEngine.Models;
using MatchingModule;
using DocumentRepresentation;
using QueryRepresentation;
using SearchEngine.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<InvertedIndex>();
builder.Services.AddSingleton<DocReader>();
builder.Services.AddSingleton<Tokenizer>();
builder.Services.AddSingleton<Ranker>();
builder.Services.AddSingleton<SearchEngineService>(); // Register your main search service as a singleton

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Search}/{action=Index}/{id?}");

app.Run();
