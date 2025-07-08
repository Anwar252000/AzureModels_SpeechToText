using azuremodels.services;
using Microsoft.AspNetCore.Mvc;

namespace azuremodels.Controllers
{
    [Route("api/[controller]")]
    public class SpeechController : ControllerBase
    {
        private readonly SpeechToTextService _speechService;
        private readonly MedicalEntityExtractionService _entityService;
        private readonly SoapNotes _soapNotes;

        public SpeechController(SpeechToTextService speechService, MedicalEntityExtractionService entityService, SoapNotes soapNotes)
        {
            _speechService = speechService;
            _entityService = entityService;
            _soapNotes = soapNotes;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ExtractFromAudio(IFormFile audioFile)
        {
            if (audioFile == null || audioFile.Length == 0)
                return BadRequest("Audio file is required.");

            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(audioFile.FileName));

            try
            {
                // Save audio to a temp file
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                    await audioFile.CopyToAsync(stream);

                // Transcribe audio
                var rawTranscription = await _speechService.TranscribeAudioWithSpeakerDiarizationUsingAzureServiceAsync(tempFilePath);
                //var transcription = "Patient is prescribed 500MG of ibrofen to be taken three times a day for seven days and 20MG of Paracetamol to be taken two times a day for 3 days, 10MG of zocor to be taken 1 time a day for 5 days and 40MG of risek to be taken three times a day for seven days, 500MG of motilium to be taken 2 times a day for one day.";

                var processedTranscription = _speechService.AssignRolesToSpeakers(rawTranscription);

                var soapFormat = _speechService.GenerateSOAPNoteAsync(processedTranscription);

                // Extract medical entities
                var entities = await _entityService.ExtractEntitiesAsync(processedTranscription);

                // Return response
                return Ok(new
                {
                    SoapFormat = soapFormat.Result,
                    Transcription = processedTranscription,
                    MedicalEntities = entities.Select(e => new
                    {
                        e.Text,
                        Category = e.Category.ToString(),
                        e.SubCategory,
                        e.ConfidenceScore
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
