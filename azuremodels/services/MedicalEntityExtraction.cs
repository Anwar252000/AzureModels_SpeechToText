using Azure;
using Azure.AI.TextAnalytics;
using System.Text;

namespace azuremodels.services
{
    public class MedicalEntityExtractionService
    {
        //TextAnalyticsClient: A client from the Azure Text Analytics SDK used to interact with the service.
        private readonly TextAnalyticsClient _client;

        public MedicalEntityExtractionService(string endpoint, string apiKey)
        {
            var credentials = new AzureKeyCredential(apiKey);
            _client = new TextAnalyticsClient(new Uri(endpoint), credentials);
        }

        public async Task<IReadOnlyList<HealthcareEntity>> ExtractEntitiesAsync(string text)
        {
            //AnalyzeHealthcareEntitiesOptions: Configures the request for healthcare entity analysis.
            //IncludeStatistics = true: Requests additional statistics about the analysis (e.g., character counts).
            var options = new AnalyzeHealthcareEntitiesOptions
            {
                IncludeStatistics = true
            };

            //Input documents: Wraps the input text in a List<string> because the API accepts multiple documents.
            var documents = new List<string> { text };

            //Start operation: Calls the API to begin analyzing healthcare entities.
            //  documents: Input documents to analyze.
            //  "en": Language code for English.
            //  options: Additional request options.
            //Returns: An AnalyzeHealthcareEntitiesOperation object that represents the long - running operation.
            var operation = await _client.StartAnalyzeHealthcareEntitiesAsync(documents, "en", options);

            // Wait for the operation to complete
            await operation.WaitForCompletionAsync();

            //Prepare a list: Collects the extracted HealthcareEntity objects.
            var entities = new List<HealthcareEntity>();

            // Use await foreach to iterate over AsyncPageable results
            await foreach (var resultCollection in operation.Value)
            {
                foreach (var result in resultCollection)
                {
                    if (result.HasError)
                    {
                        Console.WriteLine($"Error: {result.Error.Message}");
                    }
                    else
                    {
                        foreach (var entity in result.Entities)
                        {
                            Console.WriteLine($"Entity: {entity.Text}, Category: {entity.Category}, Confidence: {entity.ConfidenceScore}");
                            entities.Add(entity);
                        }
                    }
                }
            }

            return entities;
        }

        //How ExtractEntitiesAsync method Works Overall:
        //  Initializes an Azure Text Analytics client with the endpoint and API key.
        //  Configures options for healthcare entity extraction.
        //  Sends the text for analysis using the Azure Cognitive Services API.
        //  Waits for the analysis operation to complete.
        //  Processes the results to extract entities, handling errors as needed.
        //Returns the list of extracted healthcare entities.

    }
}
