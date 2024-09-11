using Microsoft.AspNetCore.Mvc;
using System.Device.Gpio;
using KaffeKlarRestAPI.Models;

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

            // Open the pin only if it is not already open
            if (!_controller.IsPinOpen(Pin))
            {
                _controller.OpenPin(Pin, PinMode.Output);
            }
        }

        [HttpGet("status")]
        public async Task<ActionResult<string>> GetStatus()
        {
            try
            {
                if (_controller.IsPinOpen(Pin))
                {
                    var pinValue = _controller.Read(Pin);
                    if (pinValue == PinValue.Low)
                    {
                        return Ok("Coffee machine in ON");
                    }
                    else
                    {
                        return Ok("Coffee machine is OFF");
                    }
                }
                else
                    return BadRequest("Pin is not open.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to fetch RPI status: {ex.Message}");
            }
        }

        [HttpPost("startcoffee")]
        public async Task<ActionResult> StartCoffeeMachine([FromBody] CoffeeRequest request)
        {
            try
            {
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

                // Log den tid, vi venter
                _logger.LogInformation($"Waiting for {timeToWait.TotalMinutes} minutes until coffee machine starts at {targetTime}");

                // Vent i det beregnede tidsinterval (ikke blokerende)
                await Task.Delay(timeToWait);

                // Start kaffemaskinen
                _controller.Write(Pin, PinValue.Low);

                // Log at kaffemaskinen er startet
                _logger.LogInformation($"Coffee machine started at {DateTime.Now}");

                await Task.Delay(300000);

                return Ok($"Coffee machine finished at {DateTime.Now}");
            }
            catch (Exception ex )
            {
                return BadRequest($"Failed to start coffee machine: {ex.Message}");
            }
        }

        [HttpPost("stopcoffee")]
        public async Task<ActionResult> StopCoffeeMachine()
        {
            try 
            {
                _controller.Write(Pin, PinValue.High);
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
