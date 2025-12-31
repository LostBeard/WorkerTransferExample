# WorkerTransferExample

This project demonatrates using [SpawnDev.BlazorJS.WebWorkers](https://github.com/LostBeard/SpawnDev.BlazorJS.WebWorkers) to run .Net code in a web worker with and without the use of the WorkerTransfer attribute. 
The WorkerTransfer attribute can be used to indicate when data should be transferred to a worker context instead of copied. 
More information about [Transferable Objects](https://developer.mozilla.org/en-US/docs/Web/API/Web_Workers_API/Transferable_objects).

[Live Demo](https://lostbeard.github.io/WorkerTransferExample/)
- The demo generates 50MB of random data and sends it to a web worker for processing.
- The demo first sends the data without using WorkerTransfer, which results in the data being copied to the worker.
- Then the demo sends the data using WorkerTransfer, which results in the data being transferred to the worker (the original data becomes detached and cannot be used anymore).
- The demo measures and displays the time taken for each operation and verifies data integrity.
- This demo requires a modern browser that supports Web Workers and Transferable Objects.
- You will notice a performance improvement when using WorkerTransfer for large data transfers.

Example output from the demo:
```
Processed without WorkerTransfer 52428800 bytes in 27 ms
ArrayBuffer is detached: False
Data integrity verified: True
Processed with WorkerTransfer 52428800 bytes in 10 ms
ArrayBuffer is detached: True
Data integrity verified: True
Done
```


### The main bits of this demo

WorkerTransferExample.csproj
```xml
    <ItemGroup>
        <PackageReference Include="SpawnDev.BlazorJS.WebWorkers" Version="2.26.0" />
    </ItemGroup>
```

Program.cs
```cs
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
```

Home.razor
```razor
@page "/"
@using SpawnDev.BlazorJS
@using SpawnDev.BlazorJS.JSObjects
@using SpawnDev.BlazorJS.WebWorkers
@using System.Security.Cryptography
@using System.Diagnostics

<PageTitle>WorkerTransfer Example</PageTitle>

<h1>WorkerTransfer Example</h1>

<button class="btn btn-primary" @onclick="Run">Click me</button>

<pre role="status">@status</pre>

@code {
    [Inject]
    WebWorkerService WebWorkerService { get; set; } = default!;

    string status = "";

    void Log(string msg)
    {
        status += msg + "\n";
        StateHasChanged();
    }

    private async Task Run()
    {
        // generate some random data for the example
        byte[] bytes = RandomNumberGenerator.GetBytes(50 * 1024 * 1024);

        // get bytes as a Uint8Array
        using var uint8Array = new Uint8Array(bytes);

        //  get the underlying ArrayBuffer
        using var arrayBufferOrig = uint8Array.Buffer;

        {
            // process the ArrayBuffer in a worker without using WorkerTransfer
            // here for comparison purposes
            var sw = Stopwatch.StartNew();
            using var arrayBufferReturned1 = await WebWorkerService.TaskPool.Run(() => ProcessFrameNoTransfer(arrayBufferOrig));
            sw.Stop();
            Log($"Processed without WorkerTransfer {arrayBufferReturned1.ByteLength} bytes in {sw.ElapsedMilliseconds} ms");

            // arrayBufferOrig is not detached and can still be used (indicates it was not transferred to the worker)
            Log($"ArrayBuffer is detached: {arrayBufferOrig.Detached}");

            // verify data integrity
            var bytesReadBack = arrayBufferReturned1.ReadBytes();
            Log($"Data integrity verified: {bytesReadBack.SequenceEqual(bytes)}");
        }

        {
            // process the ArrayBuffer in a worker using WorkerTransfer
            // the original will become detached
            var sw = Stopwatch.StartNew();
            using var arrayBufferReturned1 = await WebWorkerService.TaskPool.Run(() => ProcessFrame(arrayBufferOrig));
            sw.Stop();
            Log($"Processed with WorkerTransfer {arrayBufferReturned1.ByteLength} bytes in {sw.ElapsedMilliseconds} ms");

            // arrayBufferOrig is now detached and cannot be used (indicates it was transferred to the worker)
            Log($"ArrayBuffer is detached: {arrayBufferOrig.Detached}");

            // verify data integrity
            var bytesReadBack = arrayBufferReturned1.ReadBytes();
            Log($"Data integrity verified: {bytesReadBack.SequenceEqual(bytes)}");
        }
        Log("Done\n");
    }

    [return: WorkerTransfer]
    static async Task<ArrayBuffer> ProcessFrame([WorkerTransfer] ArrayBuffer arrayBuffer)
    {
        // write to console to indicate data was received
        Console.WriteLine($"Processing {arrayBuffer.ByteLength} bytes in worker");
        return arrayBuffer;
    }

    static async Task<ArrayBuffer> ProcessFrameNoTransfer(ArrayBuffer arrayBuffer)
    {
        // write to console to indicate data was received
        Console.WriteLine($"Processing {arrayBuffer.ByteLength} bytes in worker");
        return arrayBuffer;
    }
}
```

