# AS400 / IBM 5250 Terminal Automation - Power Automate Desktop Custom Actions

A production-ready Power Automate Desktop (PAD) Custom Action module in C# for automating AS400 / IBM i 5250 terminal sessions directly over TCP sockets (TN5250 Telnet protocol RFC 1205 / RFC 2877) with optional SSL/TLS encryption.

---

## Key Highlights

- **Pure Direct TN5250 Socket Protocol**: Connects directly to any AS400 / IBM i host over standard TCP or SSL/TLS (port 992) without needing external emulator software (e.g., IBM PCOMM, Mocha TN5250, Attachmate EXTRA!) or unmanaged HLLAPI DLLs.
- **Complete 10-Action Suite**: Write text, read screen, send control keys, wait dynamically for text, wait for screen ready, find text coordinates, and set/get cursor.
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
| **Session ID** (`SessionId`) | Output | String | - | Unique session handle used by subsequent actions. |
| **Is Connected** (`IsConnected`) | Output | Boolean | - | Returns `true` if connected and presentation space is ready. |

---

### 2. Write AS400 Text (`AS400_WriteText`)
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

### 3. Send Keys to AS400 (`AS400_SendKeys`)
Transmits typed text, tab jumps, and Attention Identification (AID) control keys (Enter, PF1-PF24, Clear, etc.) to the terminal screen. Can be executed with or without text.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle from `AS400_Connect`. |
| **Text to Send** (`TextToSend`) | Input | String | - | Keystrokes or mnemonic tags. Optional if only sending Enter or F-keys. |
| **Send Enter Key** (`SendEnterKey`) | Input | Boolean | `true` | Automatically transmits Enter AID after the text. |
| **Wait (seconds)** (`WaitSeconds`) | Input | Integer | `1` | Wait delay for screen processing after sending keys. |
| **Success** (`Success`) | Output | Boolean | - | Returns `true` if keys were transmitted successfully. |

#### Supported Mnemonic Tags:
| Mnemonic | Description | AID Code |
| :--- | :--- | :--- |
| `@E` or `[ENTER]` | Enter Key | `0xF1` |
| `@C` or `[CLEAR]` | Clear Presentation Space | `0xBD` |
| `@H` or `[HELP]` | Help Key | `0xF3` |
| `@U` or `[PAGEUP]` | Page Up (Roll Down) | `0xF4` |
| `@D` or `[PAGEDOWN]` | Page Down (Roll Up) | `0xF5` |
| `@1` to `@12` or `[PF1]` to `[PF12]` | Function Keys F1 through F12 | `0x31` - `0x3C` |
| `@13` to `@24` or `[PF13]` to `[PF24]` | Function Keys F13 through F24 | `0xB1` - `0xBC` |

---

### 4. Read AS400 Screen (`AS400_ReadScreen`)
Extracts text from the 24x80 presentation space. Supports full screen, continuous slices, or 2D rectangular bounding boxes.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Start Row** (`StartRow`) | Input | Integer | `1` | 1-based starting row coordinate (1 to 24). |
| **Start Column** (`StartCol`) | Input | Integer | `1` | 1-based starting column coordinate (1 to 80). |
| **Length** (`Length`) | Input | Integer | `1920` | Number of sequential characters (1920 reads full 24x80 screen). |
| **End Row (Optional)** (`EndRow`) | Input | Integer | - | Optional bottom row coordinate for a rectangular box. |
| **End Column (Optional)** (`EndCol`) | Input | Integer | - | Optional right column coordinate for a rectangular box. |
| **Screen Text** (`ScreenText`) | Output | String | - | Extracted text from the presentation space buffer. |

---

### 5. Wait for AS400 Text (`AS400_WaitForText`)
Waits dynamically until specific text appears on the terminal screen. Replaces fragile static sleeps with intelligent synchronization.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Text to Wait For** (`TextToWait`) | Input | String | *(Required)* | Text that signifies screen arrival (e.g., `'Customer Inquiry'`). |
| **Timeout (seconds)** (`TimeoutSeconds`) | Input | Integer | `30` | Max wait time before timing out. |
| **Target Row (Optional)** (`TargetRow`) | Input | Integer | - | Exact row (1–24). If omitted, searches anywhere on screen. |
| **Target Column (Optional)** (`TargetCol`) | Input | Integer | - | Exact column (1–80). If omitted, searches anywhere on screen. |
| **Case Sensitive** (`CaseSensitive`) | Input | Boolean | `false` | Whether to match casing strictly. |
| **Found** (`Found`) | Output | Boolean | - | Returns `true` if text appeared before timeout. |
| **Found Row** (`FoundRow`) | Output | Integer | - | 1-based row coordinate where text was matched. |
| **Found Column** (`FoundCol`) | Output | Integer | - | 1-based column coordinate where text was matched. |

---

### 6. Wait for AS400 Screen Ready (`AS400_WaitForScreenReady`)
Waits until AS400 host processing and keyboard-inhibit states clear and the terminal is receptive to input.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Timeout (seconds)** (`TimeoutSeconds`) | Input | Integer | `10` | Max wait time for ready state. |
| **Is Ready** (`IsReady`) | Output | Boolean | - | Returns `true` if screen is unlocked and ready for input. |

---

### 7. Find Text on AS400 Screen (`AS400_FindText`)
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

### 8. Set AS400 Cursor Position (`AS400_SetCursor`)
Explicitly positions the terminal cursor at specific 1-based `(Row, Column)` coordinates.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Row** (`Row`) | Input | Integer | `1` | Target row coordinate (1 to 24). |
| **Column** (`Column`) | Input | Integer | `1` | Target column coordinate (1 to 80). |
| **Success** (`Success`) | Output | Boolean | - | Returns `true` if cursor was positioned. |

---

### 9. Get AS400 Cursor Position (`AS400_GetCursor`)
Queries the current 1-based `(Row, Column)` coordinates of the terminal cursor.

| Argument | Direction | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Session ID** (`SessionId`) | Input | String | *(Required)* | Active session handle. |
| **Row** (`Row`) | Output | Integer | - | Current cursor row (1 to 24). |
| **Column** (`Column`) | Output | Integer | - | Current cursor column (1 to 80). |

---

### 10. Disconnect AS400 Session (`AS400_Disconnect`)
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

2. AS400_WaitForText
   SessionId: %SessionId%, TextToWait: "Order Entry Screen", TimeoutSeconds: 15

3. AS400_WriteText (Field 1: Customer Code)
   SessionId: %SessionId%, Row: 5, Column: 15, Text: "CUST1024", EraseLength: 10

4. AS400_WriteText (Field 2: Order Date)
   SessionId: %SessionId%, Row: 6, Column: 15, Text: "2026-09-08", EraseLength: 10

5. AS400_WriteText (Field 3: Item Code)
   SessionId: %SessionId%, Row: 8, Column: 20, Text: "ITEM_99", EraseLength: 12

6. AS400_SendKeys (Submit Form)
   SessionId: %SessionId%, TextToSend: "", SendEnterKey: True

7. AS400_WaitForText
   SessionId: %SessionId%, TextToWait: "ORDER SUBMITTED SUCCESSFULLY", TimeoutSeconds: 10

8. AS400_Disconnect
   SessionId: %SessionId%
```
