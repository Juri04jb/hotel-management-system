using HotelManagementApp.Models.DTOs;
using HotelManagementApp.Models.DTOs.HotelManagement.Models.DTOs;
using HotelManagementApp.Services.FeedbackServiceSpace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagementApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FeedbackController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;
        private readonly ILogger<FeedbackController> _logger;

        public FeedbackController(IFeedbackService feedbackService, ILogger<FeedbackController> logger)
        {
            _feedbackService = feedbackService ?? throw new ArgumentNullException(nameof(feedbackService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<IEnumerable<FeedbackDto>>> GetAllFeedback()
        {
            try
            {
                var feedback = await _feedbackService.GetAllFeedbackAsync();
                return Ok(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all feedback");
                return StatusCode(500, "An error occurred while retrieving feedback");
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Manager,Guest")]
        public async Task<ActionResult<FeedbackDto>> GetFeedbackById(int id)
        {
            try
            {
                var feedback = await _feedbackService.GetFeedbackByIdAsync(id);
                if (feedback == null)
                {
                    return NotFound($"Feedback with ID {id} not found");
                }

                // If guest role, verify the feedback belongs to them
                if (User.IsInRole("Guest"))
                {
                    var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
                    if (feedback.UserId.HasValue && feedback.UserId.Value != userId)
                    {
                        return Forbid();
                    }
                }

                return Ok(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback with ID {FeedbackId}", id);
                return StatusCode(500, "An error occurred while retrieving the feedback");
            }
        }

        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Admin,Manager,Guest")]
        public async Task<ActionResult<IEnumerable<FeedbackDto>>> GetFeedbackByUser(int userId)
        {
            try
            {
                // If guest role, verify the user is requesting their own feedback
                if (User.IsInRole("Guest"))
                {
                    var currentUserId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
                    if (userId != currentUserId)
                    {
                        return Forbid();
                    }
                }

                var feedback = await _feedbackService.GetFeedbackByUserAsync(userId);
                return Ok(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback for user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving feedback");
            }
        }

        [HttpGet("reservation/{reservationId}")]
        [Authorize(Roles = "Admin,Manager,Guest")]
        public async Task<ActionResult<IEnumerable<FeedbackDto>>> GetFeedbackByReservation(int reservationId)
        {
            try
            {
                // If guest role, verify the reservation belongs to them
                if (User.IsInRole("Guest"))
                {
                    var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
                    var reservation = await _feedbackService.GetReservationByIdAsync(reservationId);
                    if (reservation == null || reservation.UserId != userId)
                    {
                        return Forbid();
                    }
                }

                var feedback = await _feedbackService.GetFeedbackByReservationAsync(reservationId);
                return Ok(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback for reservation {ReservationId}", reservationId);
                return StatusCode(500, "An error occurred while retrieving feedback");
            }
        }

        [HttpGet("category/{category}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<IEnumerable<FeedbackDto>>> GetFeedbackByCategory(string category)
        {
            try
            {
                var feedback = await _feedbackService.GetFeedbackByCategoryAsync(category);
                return Ok(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback for category {Category}", category);
                return StatusCode(500, "An error occurred while retrieving feedback");
            }
        }

        [HttpGet("resolved/{isResolved}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<IEnumerable<FeedbackDto>>> GetFeedbackByResolutionStatus(bool isResolved)
        {
            try
            {
                var feedback = await _feedbackService.GetFeedbackByResolutionStatusAsync(isResolved);
                return Ok(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback with resolution status {IsResolved}", isResolved);
                return StatusCode(500, "An error occurred while retrieving feedback");
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<FeedbackDto>> CreateFeedback(CreateFeedbackDto createFeedbackDto)
        {
            try
            {
                int? userId = null;
                if (User.Identity?.IsAuthenticated == true)
                {
                    userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
                }

                var feedback = await _feedbackService.CreateFeedbackAsync(createFeedbackDto, userId);
                return CreatedAtAction(nameof(GetFeedbackById), new { id = feedback.Id }, feedback);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Not found: {Message}", ex.Message);
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating feedback");
                return StatusCode(500, "An error occurred while creating the feedback");
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Manager,Guest")]
        public async Task<ActionResult<FeedbackDto>> UpdateFeedback(int id, UpdateFeedbackDto updateFeedbackDto)
        {
            try
            {
                // If guest role, verify the feedback belongs to them
                if (User.IsInRole("Guest"))
                {
                    var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
                    var feedback = await _feedbackService.GetFeedbackByIdAsync(id);
                    if (feedback == null || (feedback.UserId.HasValue && feedback.UserId.Value != userId))
                    {
                        return Forbid();
                    }
                }

                var updatedFeedback = await _feedbackService.UpdateFeedbackAsync(id, updateFeedbackDto);
                return Ok(updatedFeedback);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Feedback not found: {Message}", ex.Message);
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating feedback with ID {FeedbackId}", id);
                return StatusCode(500, "An error occurred while updating the feedback");
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> DeleteFeedback(int id)
        {
            try
            {
                var result = await _feedbackService.DeleteFeedbackAsync(id);
                if (!result)
                {
                    return NotFound($"Feedback with ID {id} not found");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting feedback with ID {FeedbackId}", id);
                return StatusCode(500, "An error occurred while deleting the feedback");
            }
        }

        [HttpPost("{id}/resolve")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> ResolveFeedback(int id, [FromBody] ResolveFeedbackDto resolveDto)
        {
            try
            {
                var managerId = int.Parse(User.FindFirst("sub")?.Value ?? "0");
                var result = await _feedbackService.ResolveFeedbackAsync(id, resolveDto.ResolutionNotes, managerId);
                //if (!result)
                //{  Metoda ose kthen object ose ben throw error ska rast kur kthen asgje
                //    return NotFound($"Feedback with ID {id} not found");
                //}
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving feedback with ID {FeedbackId}", id);
                return StatusCode(500, "An error occurred while resolving the feedback");
            }
        }

        [HttpGet("stats/category")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<FeedbackStatsByCategoryDto>> GetFeedbackStatsByCategory()
        {
            try
            {
                var stats = await _feedbackService.GetFeedbackStatsByCategoryAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback statistics by category");
                return StatusCode(500, "An error occurred while retrieving feedback statistics");
            }
        }

        [HttpGet("stats/rating")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<FeedbackStatsByRatingDto>> GetFeedbackStatsByRating()
        {
            try
            {
                var stats = await _feedbackService.GetFeedbackStatsByRatingAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feedback statistics by rating");
                return StatusCode(500, "An error occurred while retrieving feedback statistics");
            }
        }

        //[HttpGet("summary")]
        //[Authorize(Roles = "Admin,Manager")]
        //public async Task<ActionResult<IEnumerable<FeedbackSummaryDto>>> GetFeedbackSummary()
        //{
        //    try
        //    {
        //        var feedbackSummary = await _feedbackService.GetFeedbackSummaryAsync();
        //        return Ok(feedbackSummary);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error retrieving feedback summary");
        //        return StatusCode(500, "An error occurred while retrieving feedback summary");
        //    }
        //}
    }

    // Additional DTO for actions that need specific data
    public class ResolveFeedbackDto
    {
        public string ResolutionNotes { get; set; }
    }
}

