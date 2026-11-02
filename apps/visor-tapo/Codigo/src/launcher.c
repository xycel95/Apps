#ifndef UNICODE
#define UNICODE
#endif
#define _UNICODE
#include <windows.h>
#include <wchar.h>

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previous, PWSTR command, int show) {
    wchar_t base[32768], python[32768], script[32768], arguments[65536];
    DWORD count = GetModuleFileNameW(NULL, base, 32768);
    if (!count || count >= 32768) return 1;
    wchar_t *slash = wcsrchr(base, L'\\');
    if (!slash) return 1;
    *slash = L'\0';
    if (swprintf(python, 32768, L"%ls\\runtime\\pythonw.exe", base) < 0 ||
        swprintf(script, 32768, L"%ls\\src\\app.py", base) < 0 ||
        swprintf(arguments, 65536, L"\"%ls\" \"%ls\"", python, script) < 0) return 1;
    if (GetFileAttributesW(python) == INVALID_FILE_ATTRIBUTES || GetFileAttributesW(script) == INVALID_FILE_ATTRIBUTES) {
        MessageBoxW(NULL, L"Extrae todo el ZIP en una carpeta y abre Visor Tapo.exe desde ella. Deben estar las carpetas runtime, src y bin.", L"Visor Tapo", MB_OK | MB_ICONINFORMATION);
        return 1;
    }
    STARTUPINFOW startup = {0};
    PROCESS_INFORMATION process = {0};
    startup.cb = sizeof(startup);
    if (!CreateProcessW(python, arguments, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, base, &startup, &process)) {
        MessageBoxW(NULL, L"No se ha podido iniciar la aplicación. Comprueba que se ha extraído todo el ZIP y que utilizas Windows de 64 bits.", L"Visor Tapo", MB_OK | MB_ICONERROR);
        return 1;
    }
    WaitForSingleObject(process.hProcess, INFINITE);
    DWORD code = 0;
    GetExitCodeProcess(process.hProcess, &code);
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    if (code) MessageBoxW(NULL, L"La aplicación se ha cerrado por un error. Revisa que las carpetas del ZIP sigan juntas. Los detalles técnicos se guardan sin contraseñas en %LOCALAPPDATA%\\VisorC230\\startup-error.txt.", L"Visor Tapo", MB_OK | MB_ICONERROR);
    return (int)code;
}
