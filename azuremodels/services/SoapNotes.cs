using Azure;
using Azure.AI.TextAnalytics;

namespace azuremodels.services
{
    public class SoapNotes
    {
        private readonly string _apiKey;
        private readonly string _endpoint;

        public SoapNotes(string apiKey, string endpoint)
        {
            _apiKey = apiKey;
            _endpoint = endpoint;
        }

        //public async Task<string> GenerateSoapNotesAsync(string conversation)
        //{
        //    using var httpClient = new HttpClient();
        //    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        //    var input = new
        //    {
        //        inputs = $@"
        //        Make SOAP notes (Subjective, Objective, Assessment, Plan) from the following conversation:
        //        {conversation}
        //    "
        //    };

        //    var content = new StringContent(JsonSerializer.Serialize(input), Encoding.UTF8, "application/json");

        //    var response = await httpClient.PostAsync(_endpoint, content);

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        throw new Exception($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
        //    }

        //    var responseContent = await response.Content.ReadAsStringAsync();
        //    return responseContent;
        //}

        public async Task<string> GenerateSOAPNoteAsync(string transcription)
        {
            // Initialize Azure Text Analytics client
            var client = new TextAnalyticsClient(new Uri(_endpoint), new AzureKeyCredential(_apiKey));

            // Wrap the transcription in a list
            var documents = new List<string> { transcription };

            // Start analyzing healthcare entities
            AnalyzeHealthcareEntitiesOperation operation = await client.StartAnalyzeHealthcareEntitiesAsync(documents);

            // Wait for the operation to complete
            await operation.WaitForCompletionAsync();

            // Use dictionaries to store categorized information
            var subjective = new List<string>();
            var objective = new List<string>();
            var assessment = new List<string>();
            var plan = new List<string>();

            // Process the results
            await foreach (AnalyzeHealthcareEntitiesResultCollection results in operation.Value)
            {
                foreach (var document in results)
                {
                    if (!document.HasError)
                    {
                        foreach (var entity in document.Entities)
                        {
                            // Classify entities into SOAP sections
                            if (entity.Category == "SymptomOrSign")
                                subjective.Add(entity.Text.Trim());
                            else if (entity.Category == "ExaminationName" || entity.Category == "MeasurementValue")
                                objective.Add(entity.Text.Trim());
                            else if (entity.Category == "Diagnosis")
                                assessment.Add(entity.Text.Trim());
                            else if (entity.Category == "TreatmentName" || entity.Category == "MedicationName" || entity.Category == "Dosage" || entity.Category == "Frequency")
                                plan.Add(entity.Text.Trim());
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Document error: {document.Error.Message}");
                    }
                }
            }

            // Helper method to construct human-readable sentences
            string FormatSection(string title, List<string> content, Func<List<string>, string> sentenceConstructor)
            {
                if (content.Count == 0)
                {
                    return $"{title}:\n(No information available)\n";
                }

                return $"{title}:\n {sentenceConstructor(content)}\n";
            }

            // Sentence construction logic for each section
            string ConstructSubjective(List<string> content)
            {
                var symptoms = string.Join(", ", content.Distinct());
                return $"The patient reported feeling {symptoms}.";
            }

            string ConstructObjective(List<string> content)
            {
                return $"The physical examination revealed the following: {string.Join(", ", content.Distinct())}.";
            }

            string ConstructAssessment(List<string> content)
            {
                return $"The following diagnoses were made: {string.Join(", ", content.Distinct())}.";
            }

            string ConstructPlan(List<string> content)
            {
                return $"The recommended plan includes: {string.Join(", ", content.Distinct())}.";
            }

            // Build the SOAP note with properly formatted sentences
            var soapNote = $@"
            SOAP Note:
            {FormatSection("Subjective", subjective, ConstructSubjective)}
            {FormatSection("Objective", objective, ConstructObjective)}
            {FormatSection("Assessment", assessment, ConstructAssessment)}
            {FormatSection("Plan", plan, ConstructPlan)}
            ";

            return soapNote.Trim();
        }

    }
}
