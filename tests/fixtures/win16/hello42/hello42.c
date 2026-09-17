/* Project-owned Win16 DLL fixture. No UI, file I/O, or application state. */
#include <windows.h>

/* Watcom's library entry stub calls LibMain during DLL initialization.
 * The NE header points at that stub; LibMain is not a named DLL export.
 */
BOOL FAR PASCAL LibMain(HINSTANCE instance, WORD dataSegment,
                       WORD heapSize, LPSTR commandLine)
{
    (void)instance;
    (void)dataSegment;
    (void)heapSize;
    (void)commandLine;
    return TRUE;
}

/* Win16 WORD is 16 bits. The result is returned in AX; FAR uses RETF.
 * __export requests the Windows exported-function prologue and public export.
 * Pascal spelling becomes HELLOWORLD; the linker assigns it ordinal 1.
 */
WORD __export FAR PASCAL HelloWorld(void)
{
    return 42;
}

/* Windows Exit Procedure: the conventional DLL unload notification. */
int FAR PASCAL WEP(int reason)
{
    (void)reason;
    return 1;
}
