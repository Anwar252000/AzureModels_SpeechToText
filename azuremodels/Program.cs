using azuremodels.services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

var azureConfig = builder.Configuration.GetSection("Azure");
builder.Services.AddSingleton(new SpeechToTextService(
    azureConfig["Speech:ApiKey"],
    azureConfig["Speech:Region"]
));

builder.Services.AddSingleton(new MedicalEntityExtractionService(
    azureConfig["TextAnalytics:Endpoint"],
    azureConfig["TextAnalytics:ApiKey"]
));

builder.Services.AddSingleton(new SoapNotes(
    azureConfig["SoapNote:ApiKey"],
    azureConfig["SoapNote:Endpoint"]
    ));

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // Frontend URLs
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
