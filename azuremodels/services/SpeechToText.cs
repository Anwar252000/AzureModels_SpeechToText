using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Transcription;
using Newtonsoft.Json;
using System.Text;

namespace azuremodels.services
{
    public class SpeechToTextService
    {
        private readonly string _apiKey;
        private readonly string _region;

        public SpeechToTextService(string apiKey, string region)
        {
            _apiKey = apiKey;
            _region = region;
        }

        public async Task<string> TranscribeAudioWithSpeakerDiarizationUsingWhisperAsync(string audioFilePath)
        {
            // Call Flask endpoint for speech-to-text
            string transcription;
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // Set a longer timeout (e.g., 5 minutes)
            };
            {
                var form = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(await System.IO.File.ReadAllBytesAsync(audioFilePath));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                form.Add(fileContent, "audio", Path.GetFileName(audioFilePath));

                var response = await client.PostAsync("http://127.0.0.1:5000/upload", form);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Flask endpoint error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                transcription = JsonConvert.DeserializeObject<dynamic>(jsonResponse)?.transcription;

                if (string.IsNullOrWhiteSpace(transcription))
                {
                    throw new Exception("Transcription failed or returned empty text.");
                }
            }

            // Azure Speaker Identification
            var config = SpeechConfig.FromSubscription(_apiKey, _region);
            using var audioConfig = AudioConfig.FromWavFileInput(audioFilePath);
            using var transcriber = new ConversationTranscriber(config, audioConfig);

            var speakerIdentifiedTranscription = new StringBuilder();
            var completionSource = new TaskCompletionSource<bool>();

            transcriber.Transcribed += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    var speakerId = e.Result.ResultId ?? "Unknown Speaker";
                    speakerIdentifiedTranscription.AppendLine($"[{speakerId}] {e.Result.Text}");
                    Console.WriteLine($"[{speakerId}] {e.Result.Text}");
                }
            };

            transcriber.Canceled += (s, e) =>
            {
                Console.WriteLine($"Speaker identification canceled: {e.Reason}, {e.ErrorDetails}");
                completionSource.TrySetResult(false);
            };

            transcriber.SessionStopped += (s, e) =>
            {
                Console.WriteLine("Speaker identification session stopped.");
                completionSource.TrySetResult(true);
            };

            // Start the transcription process for speaker identification
            await transcriber.StartTranscribingAsync();

            // Wait for speaker identification to complete
            await completionSource.Task;

            await transcriber.StopTranscribingAsync();

            // Combine transcription from Flask with speaker identification results
            return $"Flask Transcription:\n{transcription}\n\nSpeaker Identified Transcription:\n{speakerIdentifiedTranscription}";
        }

        public async Task<string> TranscribeAudioWithSpeakerDiarizationUsingAzureServiceAsync(string audioFilePath)
        {
            //SpeechConfig: Configures the Azure Speech SDK
            //FromSubscription: Initializes the SDK with an API key(_apiKey) and a region(_region)
            //These values should be set to authenticate with your Azure Speech service.
            var config = SpeechConfig.FromSubscription(_apiKey, _region);

            //AudioConfig: Specifies the audio source for the transcription.
            //FromWavFileInput: Creates an audio configuration for a.wav file, provided as audioFilePath.
            using var audioConfig = AudioConfig.FromWavFileInput(audioFilePath);

            //ConversationTranscriber: A specialized class in Azure Speech SDK for transcribing conversations with speaker identification.
            using var transcriber = new ConversationTranscriber(config, audioConfig);

            //transcription: A StringBuilder to collect the transcribed text.
            var transcription = new StringBuilder();
            //completionSource: A task used to signal the completion of the transcription process.
            var completionSource = new TaskCompletionSource<bool>();

            //e.Result: Contains transcription data
            //e.Result.Reason: Checks if the result is recognized speech.
            //e.Result.ResultId: Represents the speaker's ID (or "Unknown Speaker" if null).
            //e.Result.Text: The recognized text.
            //Action: Appends the transcription with the speaker ID and text to transcription.
            transcriber.Transcribed += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    var speakerId = e.Result.ResultId ?? "Unknown Speaker";
                    transcription.AppendLine($"[{speakerId}] {e.Result.Text}");
                    Console.WriteLine($"[{speakerId}] {e.Result.Text}");
                }
            };

            //Triggered when transcription is canceled.
            //Action: Logs the cancellation reason and signals task completion with false.
            transcriber.Canceled += (s, e) =>
            {
                Console.WriteLine($"Transcription canceled: {e.Reason}, {e.ErrorDetails}");
                completionSource.TrySetResult(false);
            };

            //Triggered when the transcription session ends.
            //Action: Logs a message and signals task completion with true.
            transcriber.SessionStopped += (s, e) =>
            {
                Console.WriteLine("Session stopped.");
                completionSource.TrySetResult(true);
            };

            //Starts the transcription process asynchronously.
            await transcriber.StartTranscribingAsync();

            // Wait until recognition completes or is canceled
            await completionSource.Task;

            //Stops the transcription process asynchronously.
            await transcriber.StopTranscribingAsync();

            //Converts the collected transcription to a string and returns it.
            return transcription.ToString();
        }
        public string AssignRolesToSpeakers(string transcription)
        {
            //speakerMap: A dictionary to map speaker IDs to their assigned roles.
            var speakerMap = new Dictionary<string, string>();
            //roleOrder: An array defining the sequence of roles to assign (e.g., first "Doctor," then "Patient").
            var roleOrder = new[] { "Doctor", "Patient" };
            //roleIndex: Tracks the current role to assign from roleOrder.
            int roleIndex = 0;

            //lines: Splits the input transcription into individual lines, removing empty entries.
            var lines = transcription.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            //processedTranscription: A StringBuilder to construct the final processed transcription.
            var processedTranscription = new StringBuilder();

            //Regex Pattern @"\[(.*?)\](.*)":
            //Matches lines with a format like [SpeakerID] Transcript text.
            //Group 1 ((.*?)) captures the speaker ID inside square brackets.
            //Group 2 ((.*)) captures the rest of the line (the text after the speaker ID).
            //Extracted Variables:
            //var speakerId: The speaker ID.
            //var text: The transcribed text after the speaker ID, trimmed of extra spaces.
            foreach (var line in lines)
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"\[(.*?)\](.*)");
                if (match.Success)
                {
                    var speakerId = match.Groups[1].Value;
                    var text = match.Groups[2].Value.Trim();

                    if (!speakerMap.ContainsKey(speakerId))
                    {
                        // Assign the next role from the role order
                        speakerMap[speakerId] = roleOrder[roleIndex % roleOrder.Length];
                        roleIndex++;
                    }

                    // Replace speaker ID with assigned role
                    var role = speakerMap[speakerId];
                    processedTranscription.AppendLine($"{role}: {text}");
                }
            }

            return processedTranscription.ToString();
        }
        //How AssignRolesToSpeakers method Works Overall:
        //1.Splits the transcription into lines.
        //2.Uses a regex to extract speaker IDs and their corresponding text.
        //3.Assigns roles to each unique speaker ID based on a predefined sequence(roleOrder).
        //4.Replaces speaker IDs with their assigned roles in the transcription.
        //5.Constructs and returns the modified transcription.

        public async Task<string> GenerateSOAPNoteAsync(string transcription)
        {
            // Initialize Azure Text Analytics client
            var client = new TextAnalyticsClient(new Uri("https://healthinfoservices.cognitiveservices.azure.com"), new AzureKeyCredential(_apiKey));

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


        public async Task<string> TranscribeWithAutoDetectAndTranslateAsync(string audioFilePath, string outputLanguage = "en")
        {
            // Initialize SpeechConfig with API key and region
            var config = SpeechConfig.FromSubscription(_apiKey, _region);

            // Enable auto-detection of source language
            var autoDetectConfig = AutoDetectSourceLanguageConfig.FromLanguages(new[] { "en-US", "ur-PK", "es-ES" });

            // Specify the audio source
            using var audioConfig = AudioConfig.FromWavFileInput(audioFilePath);
            using var recognizer = new SpeechRecognizer(config, autoDetectConfig, audioConfig);

            var transcription = new StringBuilder();
            var completionSource = new TaskCompletionSource<bool>();

            // Event triggered when a segment of speech is recognized
            recognizer.Recognized += async (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    // Get detected language
                    var autoDetectResult = AutoDetectSourceLanguageResult.FromResult(e.Result);
                    var detectedLanguage = autoDetectResult.Language;

                    Console.WriteLine($"Detected Language: {detectedLanguage}");

                    // Translate the recognized text to the output language
                    var translatedText = await TranslateTextAsync(e.Result.Text, detectedLanguage, outputLanguage);
                    transcription.AppendLine(translatedText);

                    Console.WriteLine($"Original: {e.Result.Text}");
                    Console.WriteLine($"Translated: {translatedText}");
                }
            };

            // Handle cancellation
            recognizer.Canceled += (s, e) =>
            {
                Console.WriteLine($"Recognition canceled: {e.Reason}, {e.ErrorDetails}");
                completionSource.TrySetResult(false);
            };

            // Handle session stopped
            recognizer.SessionStopped += (s, e) =>
            {
                Console.WriteLine("Session stopped.");
                completionSource.TrySetResult(true);
            };

            // Start recognizing
            await recognizer.StartContinuousRecognitionAsync();

            // Wait for recognition to complete
            await completionSource.Task;

            // Stop recognizing
            await recognizer.StopContinuousRecognitionAsync();

            return transcription.ToString();
        }

        // Helper method to translate text using Azure Translator
        private async Task<string> TranslateTextAsync(string text, string fromLanguage, string toLanguage)
        {
            var endpoint = "https://api.cognitive.microsofttranslator.com";
            var route = $"&from={fromLanguage}&to={toLanguage}";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "b82fbbf1-bb1e-457f-bc0c-dd7e0dee1eff"); // Set your Translator API key
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", _region); // Set your Translator region

            var body = new[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);

            var response = await client.PostAsync(endpoint + route, new StringContent(requestBody, Encoding.UTF8, "application/json"));
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<List<TranslationResult>>(responseBody);
            return result?.FirstOrDefault()?.Translations?.FirstOrDefault()?.Text ?? text;
        }

        // Model to parse the Translator API response
        public class TranslationResult
        {
            public List<Translation> Translations { get; set; }
        }

        public class Translation
        {
            public string Text { get; set; }
            public string To { get; set; }
        }

    }
}