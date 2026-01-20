# FlaUInspect

![FlaUInspect](src/FlaUInspect/FlaUInspect.png)

## NEW 3.0.0

New version released! Download it [here](https://github.com/FlaUI/FlaUInspect/releases/tag/v3.0.0)

This is a major update with a lot of changes:
- using separated window for process
- new UI
- Dark and Light theme support
- implement settings
- new icons
- new application selection bounding 
- redesigned:
  - property grid
  - control tree
  - menu buttons
  - mouse hover control selection
- improved performance
- fixed and redesigned 3-state highlight of selected control

#### Screenshot of new UI:

Main window and selection application highlights.

<img width="600" alt="Image" src="https://github.com/user-attachments/assets/f0a23f20-f994-4a17-b83b-3da6c96e337f" />

Main window display application and allow user to select it from list or press and hold Find window button and drag mouse over applications. The FlaUinspect will highlight the application under mouse cursor and select id in the list.

Hover mode

<img width="600" alt="Image" src="https://github.com/user-attachments/assets/53fbcd61-cd93-4f09-9962-13be900f4c95" />

Select Hover mode button and hold Ctrl key and move mouse over application windows. The FlaUInspect will highlight the control under mouse cursor and select it in the tree.

Highlight selection

<img width="600" alt="Image" src="https://github.com/user-attachments/assets/7e5c8767-e75f-449b-adb5-cbd55522f405" />

Select Selection Mode button and click on any control in the application. The FlaUInspect will highlight the selected control in the application.

Highlight selectin and Dark theme

<img width="600" alt="Image" src="https://github.com/user-attachments/assets/7b9db566-6e3b-459d-bc49-1715a172a189" />

FlaUinspect supports Light and Dark theme currently.

### 2.0.0

Download it [here](https://github.com/FlaUI/FlaUInspect/releases/tag/v2.0.0)

This is a major update with a lot of changes:

* Complete rewrite of the application
* New UI
* New features
* Much more stable
* Based on .NET 10
* Three separate versions for UIA2 and UIA3 and default with choosing on startup

### Installation

To install FlaUInspect, either build it yourself or get the zip from the releases page here on
GitHub (https://github.com/FlaUI/FlaUInspect/releases).

### Description

There are various tools around which help inspecting application that should be ui tested or automated. Some of them
are:

* VisualUIAVerify
* Inspect
* UISpy
* and probably others
  Most of them are old and sometimes not very stable and (if open source), a code mess to maintain.

FlaUInspect is supposed to be a modern alternative, based on [FlaUI](https://github.com/Roemer/FlaUI).

On startup of FlaUInspect, you can choose if you want to use UIA2 or UIA3 (
see [FAQ](https://github.com/Roemer/FlaUI/wiki/FAQ) why you can't use both at the same time).
You can use pre-built versions of FlaUInspect.UIA2 and FlaUInspect.UIA3 if you want to use a version of UIA.

###### Main Screen

![Main Screen](https://github.com/user-attachments/assets/6212341b-9776-4907-9edc-acc00073c92e)

##### Tool buttons

| Buttons        | Description                                                                                               |
|----------------|-----------------------------------------------------------------------------------------------------------|
| Hover Mode     | Enable this mode to select the item the mouse is over immediately in FlaUInspect when control is pressed  |
| Selection Mode | Selected item in tree will highlight on screen                                                            |
| Focus Tracking | Enable this mode that the focused element is always automatically selected in FlaUInspect                 |
| Show XPath     | Enable this option to show a simple XPath to the current selected element in the StatusBar of FlaUInspect |

### Release Notes 2.0.0

* Deployed FlaUInspect, FlaUInspect.UIA2, and FlaUInspect.UIA3 applications with hardcoded UI2 or UI3 selection.
* Refactored code and implemented asynchronous operations.
* Redesigned the property grid; collapsed groups remain collapsed after selecting another control.
* Added a third selection state: highlighting the selected control in an application.
* Included a refresh button for each item in the tree.
  *Refined icons.

