using KaffeklarWasmClient.Models;
using MudBlazor;
using Serilog;
using static System.Net.WebRequestMethods;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

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
                StopPressed = false;
            }
        }

        public async Task GetCoffeeMachineStatus()
        {
            Processing = true;
            HttpResponseMessage response = null;

            try
            {
                response = await Http.GetAsync("api/raspberrypi/status");

                if (response != null && response.IsSuccessStatusCode)
                {
                    // Læs indholdet af responsen korrekt
                    var content = await response.Content.ReadAsStringAsync();
                    var statusObj = JsonSerializer.Deserialize<CoffeeMachineStatus>(content);

                    if (statusObj != null && statusObj.Status == "OFF")
                    {
                        //Snackbar.Add("Kaffemaskinen er slukket", Severity.Info);
                        PowerChanged?.Invoke(PowerStatus.OFF);
                    }
                    else if (statusObj != null && statusObj.Status == "ON")
                    {
                        //Snackbar.Add("Kaffemaskinen er tændt", Severity.Info);
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
                Processing = false;
                Snackbar.Add("Kunne ikke oprette forbindelse til serveren", Severity.Error);
                PowerChanged?.Invoke(PowerStatus.UNKNOWN);
                Log.Warning(ex.ToString());
            }
        }
    }
}
