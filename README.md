# AS400 / IBM 5250 Terminal Automation - Power Automate Desktop Custom Actions

A production-ready Power Automate Desktop (PAD) Custom Action module in C# for automating AS400 / IBM i 5250 terminal sessions directly over TCP sockets (TN5250 Telnet protocol RFC 1205 / RFC 2877) with optional SSL/TLS encryption.

---

## Key Highlights

- **Pure Direct TN5250 Socket Protocol**: Connects directly to any AS400 / IBM i host over standard TCP or SSL/TLS (port 992) without needing external emulator software (e.g., IBM PCOMM, Mocha TN5250, Attachmate EXTRA!) or unmanaged HLLAPI DLLs.
- **Complete 11-Action Suite**: Connect, Disconnect, Write text, Send key (with drop-down menu), Read all screen text, Read screen slice/box, Wait for text to appear (whole screen), Wait for screen ready, Find text coordinates, and Set/Get cursor.
- **Form Filling Before Submitting (`Write Text`)**: Directly write into multiple fields across the screen without premature submits.
- **Fail-Safe Disconnection & Cleanup**: Guaranteed teardown of TCP sockets, streams, background listener threads, and in-memory session handles on timeouts, network drops, or unhandled errors.
- **Enterprise-Grade Presentation Space**: Maintains a thread-safe 24x80 presentation space buffer, bidirectional EBCDIC (Code Page 037) encoding, 5250 data stream order parser (SOH, RA, SBA, IC, SF, Clear Unit, WTD), and AID keystroke engine.
- **PAD SDK Compliant**: Built with `Microsoft.PowerPlatform.PowerAutomate.Desktop.Actions.SDK`, targeting .NET Framework 4.7.2, and following official PAD naming conventions (`Modules.AS400PADCustomAction.dll`).
- **One-Click Build & Packaging**: Automated PowerShell packaging script (`Build-And-Package.ps1`) supporting Release compilation, `makecab.exe` CAB bundling, and fast offline SHA256 digital code signing.

---

## Action Reference (Category: `AS400 Automation`)

### 1. Connect AS400 Session (`AS400_Connect`)
Establishes a direct TN5250 connection to the AS400 host, completes the Telnet negotiation handshake (`TERMINAL-TYPE IBM-3179-2`, `TRANSMIT-BINARY`, `END-OF-RECORD`), and initializes the presentation space.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Host Name / IP** (`Host`) | Input | String | *(Required)* | Hostname or IP address of the AS400 system. |
| **Port** (`Port`) | Input | Integer | `23` | Telnet port (23 for standard, 992 for SSL). |
| **Session Short Name / ID** (`SessionName`) | Input | String | `"A"` | Prefix for the generated session handle (e.g. 'A'). |
| **Timeout (seconds)** (`TimeoutSeconds`) | Input | Integer | `30` | Max wait time for TCP connect and initial screen render. |
| **Use SSL / TLS** (`UseSsl`) | Input | Boolean | `false` | Enable secure SSL/TLS communication. |
| **Accept Any Certificate** (`AcceptAnyCertificate`) | Input | Boolean | `false` | Accepts internal CA or self-signed AS400 certificates. |
| **Show Live Terminal Viewer** (`ShowLiveViewer`) | Input | Boolean | `false` | If true, launches a floating 24x80 green-screen emulator window. |
| **Session ID** (`SessionId`) | Output | String | - | Unique session handle used by subsequent actions. |
| **Is Connected** (`IsConnected`) | Output | Boolean | - | Returns `true` if connected and presentation space is ready. |

---

### 2. Show AS400 Terminal Viewer (`AS400_ShowViewer`)
Opens, closes, or toggles the floating real-time AS400 live terminal emulator window for an active session. Shows the live 24x80 screen, cursor location, and flow action step status in real time.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle from `AS400_Connect`. |
| **Show Viewer** (`Show`) | Input | Boolean | `true` | If true, opens/focuses the live viewer. If false, closes it. |
| **Always on Top** (`AlwaysOnTop`) | Input | Boolean | `true` | Pins the viewer window above all other desktop applications. |
| **Success** (`Success`) | Output | Boolean | - | Returns `true` if the viewer state was updated. |

---

### 3. Write AS400 Text (`AS400_WriteText`)
Writes text into the terminal screen at specified `(Row, Column)` coordinates or at the current cursor position **without submitting the screen**. This allows filling in multiple fields across a form before pressing Enter.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle from `AS400_Connect`. |
| **Text to Write** (`Text`) | Input | String | - | Text value to enter into the field. |
| **Row (Optional)** (`Row`) | Input | Integer | *(Current)* | 1-based target row (1–24). If omitted, uses current cursor row. |
| **Column (Optional)** (`Column`) | Input | Integer | *(Current)* | 1-based target column (1–80). If omitted, uses current cursor column. |
| **Erase Field Length** (`EraseLength`) | Input | Integer | `0` | Number of characters to blank with spaces before writing (0 disables). |
| **Success** (`Success`) | Output | Boolean | - | Returns `true` if text was successfully written. |
| **New Row** (`NewRow`) | Output | Integer | - | Cursor row coordinate after writing. |
| **New Column** (`NewCol`) | Output | Integer | - | Cursor column coordinate after writing. |

---

### 4. Send Key to AS400 (`AS400_SendKey`)
Transmits a function or control key to the active AS400 terminal session directly using a native **Power Automate Desktop drop-down menu**. Eliminates the need to memorize mnemonic codes.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle from `AS400_Connect`. |
| **Key to Send** (`Key`) | Input | Dropdown (`AS400Key`) | `Enter` | Drop-down list: `Enter`, `F1`–`F24`, `PageUp`, `PageDown`, `Clear`, `Help`, `Print`, `RecordBackspace`. |
| **Wait (seconds)** (`WaitSeconds`) | Input | Integer | `1` | Wait delay for screen processing after sending key. |
| **Success** (`Success`) | Output | Boolean | - | Returns `true` if key was transmitted successfully. |

---

### 5. Read All Screen Text (`AS400_ReadAllScreenText`)
Reads all text across the entire 24x80 AS400 presentation space in one operation.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Preserve Line Breaks** (`PreserveLineBreaks`) | Input | Boolean | `true` | If true, formats rows separated by line breaks. If false, returns continuous string. |
| **Screen Text** (`ScreenText`) | Output | String | - | Complete text extracted from the current AS400 screen. |

---

### 6. Read AS400 Screen (`AS400_ReadScreen`)
Extracts specific text from the 24x80 presentation space. Supports linear slices or 2D rectangular bounding boxes.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Start Row** (`StartRow`) | Input | Integer | `1` | 1-based starting row coordinate (1 to 24). |
| **Start Column** (`StartCol`) | Input | Integer | `1` | 1-based starting column coordinate (1 to 80). |
| **Length** (`Length`) | Input | Integer | `1920` | Number of sequential characters (1920 reads full screen). |
| **End Row (Optional)** (`EndRow`) | Input | Integer | - | Optional bottom row coordinate for a rectangular box. |
| **End Column (Optional)** (`EndCol`) | Input | Integer | - | Optional right column coordinate for a rectangular box. |
| **Screen Text** (`ScreenText`) | Output | String | - | Extracted text from the presentation space buffer. |

---

### 7. Wait for Text to Appear (`AS400_WaitForTextToAppear`)
Waits dynamically until specific text appears anywhere on the current screen. Eliminates fragile static sleeps with intelligent whole-screen synchronization (no row/column input required).

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Text to Wait For** (`TextToWait`) | Input | String | *(Required)* | Text that signifies screen arrival (e.g., `'Customer Inquiry'`, `'Sign On'`). |
| **Timeout (seconds)** (`TimeoutSeconds`) | Input | Integer | `30` | Max wait time before timing out. |
| **Case Sensitive** (`CaseSensitive`) | Input | Boolean | `false` | Whether to match casing strictly. |
| **Found** (`Found`) | Output | Boolean | - | Returns `true` if text appeared before timeout. |
| **Found Row** (`FoundRow`) | Output | Integer | - | 1-based row coordinate where text was matched. |
| **Found Column** (`FoundCol`) | Output | Integer | - | 1-based column coordinate where text was matched. |

---

### 8. Wait for AS400 Screen Ready (`AS400_WaitForScreenReady`)
Waits until AS400 host processing and keyboard-inhibit states clear and the terminal is receptive to input.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Timeout (seconds)** (`TimeoutSeconds`) | Input | Integer | `10` | Max wait time for ready state. |
| **Is Ready** (`IsReady`) | Output | Boolean | - | Returns `true` if screen is unlocked and ready for input. |

---

### 9. Find Text on AS400 Screen (`AS400_FindText`)
Searches the 24x80 presentation space for a string and returns its exact `(Row, Column)` coordinates for dynamic field navigation.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Search Text** (`SearchText`) | Input | String | *(Required)* | Text string to find on screen. |
| **Start Row** (`StartRow`) | Input | Integer | `1` | Starting row to begin search from. |
| **Start Column** (`StartCol`) | Input | Integer | `1` | Starting column to begin search from. |
| **Case Sensitive** (`CaseSensitive`) | Input | Boolean | `false` | Case sensitivity toggle. |
| **Found** (`Found`) | Output | Boolean | - | Returns `true` if text exists on screen. |
| **Found Row** (`FoundRow`) | Output | Integer | - | 1-based row index (0 if not found). |
| **Found Column** (`FoundCol`) | Output | Integer | - | 1-based column index (0 if not found). |

---

### 10. Set AS400 Cursor Position (`AS400_SetCursor`)
Explicitly positions the terminal cursor at specific 1-based `(Row, Column)` coordinates.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Row** (`Row`) | Input | Integer | `1` | Target row coordinate (1 to 24). |
| **Column** (`Column`) | Input | Integer | `1` | Target column coordinate (1 to 80). |
| **Success** (`Success`) | Output | Boolean | - | Returns `true` if cursor was positioned. |

---

### 11. Get AS400 Cursor Position (`AS400_GetCursor`)
Queries the current 1-based `(Row, Column)` coordinates of the terminal cursor.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Row** (`Row`) | Output | Integer | - | Current cursor row (1 to 24). |
| **Column** (`Column`) | Output | Integer | - | Current cursor column (1 to 80). |

---

### 12. Disconnect AS400 Session (`AS400_Disconnect`)
Cleanly terminates the terminal session, closes network socket streams, and releases registry handles.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle to disconnect. |
| **Success** (`Success`) | Output | Boolean | - | Returns `true` on successful disconnection. |

---

## Building and Packaging

### Quick Build & Package (Automated)

Run the included packaging script with the `-CreateSelfSignedCert` switch:

```powershell
cd D:\PROJECTS\AS400PADCustomAction
.\Build-And-Package.ps1 -CreateSelfSignedCert
```

This compiles `Modules.AS400PADCustomAction.dll` in `Release`, creates `dist\Modules.AS400PADCustomAction.cab`, and applies SHA256 code signing in under 5 seconds.

### Installing Test Certificate Locally
```cmd
certutil -addstore -user Root "D:\PROJECTS\AS400PADCustomAction\dist\AS400PADCustomAction_TestCert.cer"
```

### Uploading to Power Automate Portal
1. Navigate to **[make.powerautomate.com](https://make.powerautomate.com/)**.
2. Go to **More** > **Discover all** > **Assets library** > **Custom actions**.
3. Click **Upload custom action** and select:
   `D:\PROJECTS\AS400PADCustomAction\dist\Modules.AS400PADCustomAction.cab`

---

## Example Flow Walkthrough (Multi-Field Form Filling)

```text
1. AS400_Connect
   Host: "192.168.1.100", Port: 23, SessionName: "A"
   --> Outputs: %SessionId%

2. AS400_WaitForTextToAppear
   SessionId: %SessionId%, TextToWait: "Order Entry Screen", TimeoutSeconds: 15

3. AS400_WriteText (Field 1: Customer Code)
   SessionId: %SessionId%, Row: 5, Column: 15, Text: "CUST1024", EraseLength: 10

4. AS400_WriteText (Field 2: Order Date)
   SessionId: %SessionId%, Row: 6, Column: 15, Text: "2026-09-08", EraseLength: 10

5. AS400_WriteText (Field 3: Item Code)
   SessionId: %SessionId%, Row: 8, Column: 20, Text: "ITEM_99", EraseLength: 12

6. AS400_SendKey (Submit Form via Enter Dropdown)
   SessionId: %SessionId%, Key: Enter, WaitSeconds: 1

7. AS400_WaitForTextToAppear
   SessionId: %SessionId%, TextToWait: "ORDER SUBMITTED SUCCESSFULLY", TimeoutSeconds: 10

8. AS400_ReadAllScreenText (Capture Final Confirmation Screen)
   SessionId: %SessionId%, PreserveLineBreaks: True
   --> Outputs: %ScreenText%

9. AS400_Disconnect
   SessionId: %SessionId%
```
