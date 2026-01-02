# WorkerTransferExample

This project demonatrates using [SpawnDev.BlazorJS.WebWorkers](https://github.com/LostBeard/SpawnDev.BlazorJS.WebWorkers) to run .Net code in a web worker with and without the use of the WorkerTransfer attribute. 
The WorkerTransfer attribute can be used to indicate when data should be transferred to a worker context instead of copied. 
More information about [Transferable Objects](https://developer.mozilla.org/en-US/docs/Web/API/Web_Workers_API/Transferable_objects).

[Live Demo](https://lostbeard.github.io/WorkerTransferExample/)
- The demo generates 50MB of random data and sends it to a web worker for processing.
- Test 1 The demo first sends the data as an ArrayBuffer without using WorkerTransfer, which results in the data being copied to the worker.
- Test 2 Then the demo sends the data as an ArrayBuffer using WorkerTransfer, which results in the data being transferred to the worker (the original data becomes detached and cannot be used anymore).
- Test 3 Then the demo sends the data as a byte[]. byte[] is copied to Javascript becoming a Uint8Array and its ArrayBuffer is transferred to the worker to prevent an extra copy.
- The demo measures and displays the time taken for each operation.
- This demo requires a modern browser that supports Web Workers and Transferable Objects.
- You will notice a performance improvement when using WorkerTransfer for large data transfers.

Example output from the demo:
```
ArrayBuffer without WorkerTransfer...
ArrayBuffer is detached: False
Processed without WorkerTransfer 52428800 bytes in 119 ms

ArrayBuffer with WorkerTransfer...
ArrayBuffer is detached: True
Processed with WorkerTransfer 52428800 bytes in 52 ms

byte[] - copied to Javascript and transferred to worker...
byte[] is detached: [Not Applicable]
Processed byte[] 52428800 bytes in 52 ms
```


### The main bits of this demo

WorkerTransferExample.csproj
```xml
    <ItemGroup>
        <PackageReference Include="SpawnDev.BlazorJS.WebWorkers" Version="2.27.0" />
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

<button class="btn btn-primary" @onclick="Run">Run</button>

<pre role="status">@status</pre>

@code {
    [Inject]
    WebWorkerService WebWorkerService { get; set; } = default!;

    string status = "";

    // generate some random data for the example
    byte[] bytes = RandomNumberGenerator.GetBytes(50 * 1024 * 1024);

    void Log(string msg)
    {
        status += msg + "\n";
        StateHasChanged();
    }

    void LogClear()
    {
        status = "";
        StateHasChanged();
    }

    private async Task Run()
    {
        LogClear();

        {
            Log($"ArrayBuffer without WorkerTransfer...");

            // process the ArrayBuffer in a worker without using WorkerTransfer
            // here for comparison purposes
            var sw = Stopwatch.StartNew();

            // get bytes as a Uint8Array
            using var uint8Array = new Uint8Array(bytes);

            //  get the underlying ArrayBuffer
            using var arrayBufferOrig = uint8Array.Buffer;
            using var arrayBufferReturned1 = await WebWorkerService.TaskPool.Run(() => ProcessFrameNoTransfer(arrayBufferOrig));

            // arrayBufferOrig is not detached and can still be used (indicates it was not transferred to the worker)
            Log($"ArrayBuffer is detached: {arrayBufferOrig.Detached}");

            // pull back into .Net so it more fairly compares to the byte[] method
            var bytesReadBack = arrayBufferReturned1.ReadBytes();

            sw.Stop();
            Log($"Processed without WorkerTransfer {arrayBufferReturned1.ByteLength} bytes in {sw.ElapsedMilliseconds} ms\n");
        }

        {
            Log($"ArrayBuffer with WorkerTransfer...");

            // process the ArrayBuffer in a worker using WorkerTransfer
            // the original will become detached
            var sw = Stopwatch.StartNew();

            // get bytes as a Uint8Array
            using var uint8Array = new Uint8Array(bytes);

            //  get the underlying ArrayBuffer
            using var arrayBufferOrig = uint8Array.Buffer;
            using var arrayBufferReturned1 = await WebWorkerService.TaskPool.Run(() => ProcessFrame(arrayBufferOrig));

            // arrayBufferOrig is now detached and cannot be used (indicates it was transferred to the worker)
            Log($"ArrayBuffer is detached: {arrayBufferOrig.Detached}");

            // pull back into .Net so it more fairly compares to the byte[] method
            var bytesReadBack = arrayBufferReturned1.ReadBytes();

            sw.Stop();
            Log($"Processed with WorkerTransfer {arrayBufferReturned1.ByteLength} bytes in {sw.ElapsedMilliseconds} ms\n");
        }

        {
            Log($"byte[] - transferable and ArrayBuffer used by the dispatcher automatically...");

            // process the byte[] in a worker (while byte[] itself is not transferrable, transferrable is used under the hood to prevent copying where possible)
            // here for comparison purposes
            var sw = Stopwatch.StartNew();
            var bytesReadBack = await WebWorkerService.TaskPool.Run(() => ProcessFrameByteArray(bytes));

            // arrayBufferOrig is not used directly. write to the log like the other tests do.
            Log($"ArrayBuffer is detached: {true}");

            // the data is already in .Net (other methods do it manually for a fair comparison)

            sw.Stop();
            Log($"Processed byte[] {bytesReadBack.Length} bytes in {sw.ElapsedMilliseconds} ms\n");
        }
    }

    [return: WorkerTransfer]
    static async Task<ArrayBuffer> ProcessFrame([WorkerTransfer] ArrayBuffer arrayBuffer)
    {
        // read in the data and write back out so it compares more equally with the other methods
        byte[] data = arrayBuffer.ReadBytes();
        ArrayBuffer retunrnedArrayBuffer = new Uint8Array(data).Buffer;
        Console.WriteLine($"Processing ArrayBuffer with WorkerTransfer {data.Length} bytes in worker");
        return retunrnedArrayBuffer;
    }

    static async Task<ArrayBuffer> ProcessFrameNoTransfer(ArrayBuffer arrayBuffer)
    {
        // read in the data and write back out so it compares more equally with the other methods
        byte[] data = arrayBuffer.ReadBytes();
        ArrayBuffer retunrnedArrayBuffer = new Uint8Array(data).Buffer;
        Console.WriteLine($"Processing ArrayBuffer without WorkerTransfer {data.Length} bytes in worker");
        return retunrnedArrayBuffer;
    }

    static async Task<byte[]> ProcessFrameByteArray(byte[] data)
    {
        // write to console to indicate data was received
        Console.WriteLine($"Processing byte[] {data.Length} bytes in worker");
        return data;
    }
}
```

