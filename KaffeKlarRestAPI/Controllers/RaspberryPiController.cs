using Microsoft.AspNetCore.Mvc;
using System.Device.Gpio;
using SharedComponents;
namespace KaffeKlarRestAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RaspberryPiController : ControllerBase
    {
        private readonly ILogger<RaspberryPiController> _logger;
        private readonly GpioController _controller;
        private const int Pin = 21;

        public RaspberryPiController(ILogger<RaspberryPiController> logger, GpioController controller)
        {
            _logger = logger;
            _controller = controller;
        }

        [HttpGet("status")]
        public async Task<ActionResult<CoffeeMachineStatus>> GetStatus()
        {
            string status;
            try
            {
                if (_controller.IsPinOpen(Pin))
                {
                    var pinValue = _controller.Read(Pin);
                    status = pinValue == PinValue.Low ? "ON" : "OFF";

                    // Returner status som JSON
                    return Ok(new CoffeeMachineStatus { Status = status });
                }
                else
                {
                    status = "Pin not open";
                    return Ok(new CoffeeMachineStatus { Status = status });
                }
            }
            catch (Exception ex)
            {
                status = $"Failed to fetch RPI status: {ex.Message}";
                return BadRequest(new CoffeeMachineStatus { Status = status });
            }
        }

        [HttpPost("startcoffee")]
        public ActionResult StartCoffeeMachine([FromBody] CoffeeRequest request)
        {
            try
            {
                var now = DateTime.Now;
                var selectedTime = request.Time ?? TimeSpan.Zero;
                var targetTime = now.Date.Add(selectedTime);

                // Assume timer for next day, if target time is before current time
                if (targetTime <= now)
                {
                    targetTime = targetTime.AddDays(1);
                }

                // Compute time difference
                var timeToWait = targetTime - now;

                _logger.LogInformation($"Scheduled to start coffee machine in {timeToWait.TotalMinutes} minutes at {targetTime}");

                // Start en baggrundsopgave for at vente og derefter starte kaffemaskinen
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(timeToWait);

                        // We open pin just before turning it on to mitigate risk of residual power in relay
                        // Alternative solution: Open pin in constructor, but immediately write PinValue.High so power off relay
                        if (!_controller.IsPinOpen(Pin))
                        {
                            _controller.OpenPin(Pin, PinMode.Output);
                            _logger.LogInformation($"Pin {Pin} was opened.");
                        }

                        _controller.Write(Pin, PinValue.Low);
                        _logger.LogInformation($"Coffee machine started at {DateTime.Now}");

                        _logger.LogInformation("Waiting 10 minutes for coffee machine to complete...");
                        // Bug: If coffee machine is manually shut down via app while still waiting for the task to complete
                        // The task will complete the next time the coffee machine is activated and shut it down immediately
                        await Task.Delay(600000);

                        await StopCoffeeMachine();

                        _logger.LogInformation($"Coffee machine finished at {DateTime.Now}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to start coffee machine: {ex.Message}");
                    }
                });

                // Returnér straks svar til klienten
                return Ok($"Coffee machine will start at {targetTime}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to schedule coffee machine: {ex.Message}");
            }
        }


        [HttpPost("stopcoffee")]
        public async Task<ActionResult> StopCoffeeMachine()
        {
            try 
            {
                _controller.Write(Pin, PinValue.High);
                _controller.ClosePin(Pin);
                return Ok("Coffee machine stopped");
            }
            catch ( Exception ex )
            {
                return BadRequest($"Failed to stop coffee machine: {ex.Message}");
            }
        }

        [NonAction]
        public void Dispose()
        {
            if (_controller.IsPinOpen(Pin))
            {
                _controller.ClosePin(Pin); // Close the pin when done
                _logger.LogInformation($"Pin {Pin} was closed");
            }
            _controller.Dispose(); // Dispose of the GpioController
        }
    }
}
