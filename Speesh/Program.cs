using Speesh.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Register conversation history as a singleton so session history is preserved across requests
builder.Services.AddSingleton<ConversationHistoryService>();

// Configure CORS: allow any origin, header and method (for development / public API)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Serve static files from wwwroot and make Speech_front-end.htm the default document at '/'
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = { "Speech_front-end.htm" }
});
app.UseStaticFiles();

// Enable Swagger UI at '/swagger' so the application root can serve the static frontend
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger"; // serve Swagger UI at '/swagger'
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Speesh API V1");
});

app.UseHttpsRedirection();

// Enable CORS globally using the "AllowAll" policy
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
