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
                    status = "Pin is not open.";
                    return BadRequest(new CoffeeMachineStatus { Status = status });
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
                // Open the pin only if it is not already open
                if (!_controller.IsPinOpen(Pin))
                {
                    _controller.OpenPin(Pin, PinMode.Output);
                }
                // Det aktuelle tidspunkt
                var now = DateTime.Now;

                // Tiden som modtages fra Blazor-appen
                var selectedTime = request.Time ?? TimeSpan.Zero;

                // Opret en DateTime for den ønskede tid i dag
                var targetTime = now.Date.Add(selectedTime);

                // Hvis den ønskede tid er tidligere på dagen end det nuværende tidspunkt,
                // antager vi, at det er den næste dag
                if (targetTime <= now)
                {
                    targetTime = targetTime.AddDays(1);
                }

                // Beregn forskellen mellem nu og den ønskede tid
                var timeToWait = targetTime - now;

                // Log ventetiden
                _logger.LogInformation($"Scheduled to start coffee machine in {timeToWait.TotalMinutes} minutes at {targetTime}");

                // Start en baggrundsopgave for at vente og derefter starte kaffemaskinen
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(timeToWait);

                        // Start kaffemaskinen
                        _controller.Write(Pin, PinValue.Low);

                        // Log at kaffemaskinen er startet
                        _logger.LogInformation($"Coffee machine started at {DateTime.Now}");

                        // Vent 10 minutter (600.000 ms) for kaffemaskinens fuldførelse
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
                return Ok("Coffee machine stopeed");
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
                _logger.LogInformation("Pin was closed");
            }
            _controller.Dispose(); // Dispose of the GpioController
        }
    }
}
