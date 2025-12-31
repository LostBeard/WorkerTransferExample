using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.WebWorkers;
using WorkerTransferExample;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddBlazorJSRuntime(out var JS);
builder.Services.AddWebWorkerService(webWorkerService =>
{
    // warm up a single worker in the pool
    webWorkerService.TaskPool.PoolSize = 1;
});
if (JS.IsWindow)
{
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
}
await builder.Build().BlazorJSRunAsync();
