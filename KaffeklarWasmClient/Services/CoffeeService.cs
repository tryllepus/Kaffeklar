using MudBlazor;
using Serilog;
using static System.Net.WebRequestMethods;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using SharedComponents;

namespace KaffeklarWasmClient.Services
{
    public class CoffeeService
    {
        public TimeSpan? CurrentTime { get; set; }
        public bool Processing { get; set; } = false;
        public bool StartPressed { get; set; } = false;
        public bool StopPressed { get; set; } = false;
        public Action<PowerStatus> PowerChanged { get; set; }
        private HttpClient Http;
        private ISnackbar Snackbar;

        public CoffeeService(HttpClient http, ISnackbar snackbar) 
        {
            Http = http;
            Snackbar = snackbar;
        }

        public async Task StartCoffeeMachine()
        {
            StartPressed = true;
            var coffeeRequest = new CoffeeRequest { Time = CurrentTime };
            HttpResponseMessage response = null;

            // Konverter objektet til JSON-indhold
            var jsonContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(coffeeRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            try
            {
                // Forsøger at sende POST-anmodningen
                response = await Http.PostAsync("api/raspberrypi/startcoffee", jsonContent);

                // Tjek om anmodningen lykkedes, før der vises en succesbesked
                if (response != null && response.IsSuccessStatusCode)
                {
                    Snackbar.Add($"Kaffemaskinen starter kl. {CurrentTime}", Severity.Success);
                }
                else
                {
                    Snackbar.Add("Fejl ved start af kaffemaskinen", Severity.Error);
                }
                await GetCoffeeMachineStatus();
            }
            catch (Exception ex)
            {
                // Hvis der opstår en undtagelse, vis en fejlbesked
                Snackbar.Add("Kunne ikke oprette forbindelse til serveren", Severity.Error);
                Log.Warning(ex.ToString());
            }
            finally
            {
                StartPressed = false;
            }
        }


        public async Task StopCoffeeMachine()
        {
            HttpResponseMessage response = null;
            StopPressed = true;

            try
            {
                response = await Http.PostAsync("api/raspberrypi/stopcoffee", null);

                // Tjek om anmodningen lykkedes, før der vises en succesbesked
                if (response != null && response.IsSuccessStatusCode)
                {
                    Snackbar.Add("Kaffemaskinen blev stoppet", Severity.Success);
                }
                else
                {
                    Snackbar.Add("Fejl ved stop af kaffemaskinen", Severity.Error);
                }
                await GetCoffeeMachineStatus();
            }
            catch (Exception ex)
            {
                Snackbar.Add("Kunne ikke oprette forbindelse til serveren", Severity.Error);
                Log.Warning(ex.ToString());
            }
            finally
            {
                StopPressed = false;
            }
        }

        public async Task GetCoffeeMachineStatus()
        {
            Processing = true;
            HttpResponseMessage response = null;
            CoffeeMachineStatus coffeeMachineStatus = null;

            try
            {
                response = await Http.GetAsync("api/raspberrypi/status");

                var content = await response.Content.ReadAsStringAsync();
                Log.Information($"Response content: {content}");

                coffeeMachineStatus = JsonSerializer.Deserialize<CoffeeMachineStatus>(content);
                if (coffeeMachineStatus == null)
                {
                    Log.Error("Failed to deserialize coffee machine status.");
                }

                if (response != null && response.IsSuccessStatusCode)
                {
                    // Læs indholdet af responsen korrekt

                    if (coffeeMachineStatus != null && coffeeMachineStatus.Status == "OFF")
                    {
                        PowerChanged?.Invoke(PowerStatus.OFF);
                    }
                    else if (coffeeMachineStatus != null && coffeeMachineStatus.Status == "ON")
                    {
                        PowerChanged?.Invoke(PowerStatus.ON);
                    }
                }
                else
                {
                    Snackbar.Add("Fejl ved læsning af status", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Kunne ikke oprette forbindelse til serveren: {ex.Message}", Severity.Error);
                PowerChanged?.Invoke(PowerStatus.UNKNOWN);
                Log.Warning($"Error while calling CoffeeService.GetCoffeeMachineStatus {ex}");
            }
            finally
            {
                Processing = false;
            }
        }
    }
}
