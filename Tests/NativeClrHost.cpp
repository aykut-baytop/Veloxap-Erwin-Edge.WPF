#include <windows.h>
#include <metahost.h>
#include <shellscalingapi.h>
#include <stdio.h>

#pragma comment(lib, "mscoree.lib")
#pragma comment(lib, "shcore.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "ole32.lib")

// A native, initially DPI-unaware host with no managed entry assembly, like erwin.
int wmain(int argc, wchar_t** argv)
{
    if (argc != 4)
        return 2;

    const bool baseline = wcscmp(argv[3], L"baseline") == 0;
    if (wcscmp(argv[3], L"system-aware") == 0)
        SetProcessDpiAwareness(PROCESS_SYSTEM_DPI_AWARE);
    else if (wcscmp(argv[3], L"per-monitor-v2") == 0)
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    HWND owner = CreateWindowExW(0, L"STATIC", L"Native erwin test host",
        WS_OVERLAPPEDWINDOW, 100, 100, 800, 600, nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
    if (!owner)
        return 3;

    PROCESS_DPI_AWARENESS before, after;
    GetProcessDpiAwareness(GetCurrentProcess(), &before);
    auto contextBefore = GetThreadDpiAwarenessContext();
    RECT boundsBefore, boundsAfter;
    GetWindowRect(owner, &boundsBefore);

    ICLRMetaHost* meta = nullptr;
    ICLRRuntimeInfo* runtime = nullptr;
    ICLRRuntimeHost* host = nullptr;
    HRESULT result = CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, reinterpret_cast<void**>(&meta));
    if (SUCCEEDED(result))
        result = meta->GetRuntime(L"v4.0.30319", IID_ICLRRuntimeInfo, reinterpret_cast<void**>(&runtime));
    if (SUCCEEDED(result))
        result = runtime->GetInterface(CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, reinterpret_cast<void**>(&host));
    if (SUCCEEDED(result))
        result = host->Start();

    DWORD testResult = 1;
    if (SUCCEEDED(result))
    {
        wchar_t argument[32768];
        swprintf_s(argument, L"%s|%s", argv[2], argv[3]);
        result = host->ExecuteInDefaultAppDomain(argv[1], L"WpfHostingSmokeTest", L"Run", argument, &testResult);
    }

    GetProcessDpiAwareness(GetCurrentProcess(), &after);
    auto contextAfter = GetThreadDpiAwarenessContext();
    GetWindowRect(owner, &boundsAfter);
    const bool unchanged = before == after && AreDpiAwarenessContextsEqual(contextBefore, contextAfter)
        && EqualRect(&boundsBefore, &boundsAfter);
    const bool passed = SUCCEEDED(result) && testResult == 0
        && (baseline ? before == PROCESS_DPI_UNAWARE && after == PROCESS_SYSTEM_DPI_AWARE : unchanged);
    wprintf(L"%s: DPI %d -> %d, bounds %s, HRESULT 0x%08X, test %lu: %s\n",
        argv[3], before, after, EqualRect(&boundsBefore, &boundsAfter) ? L"unchanged" : L"changed",
        static_cast<unsigned>(result), testResult, passed ? L"PASS" : L"FAIL");

    if (host) host->Release();
    if (runtime) runtime->Release();
    if (meta) meta->Release();
    DestroyWindow(owner);
    CoUninitialize();
    return passed ? 0 : 1;
}
