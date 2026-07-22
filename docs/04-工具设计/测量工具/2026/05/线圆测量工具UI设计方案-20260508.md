# Line-Circle Measurement Tool UI Design

## 1. Overview

The Line-Circle measurement tool computes the distance from a line to a circle.
It supports two measurement modes: Center-to-Line distance and Shortest distance.
The user can configure line and circle input sources, measurement parameters,
tolerance settings, and result display options.

The UI uses a three-tab layout: Basic Parameters, Measurement Parameters,
and Result Display.

## 2. Overall Layout

The tool window follows a standard tabbed dialog pattern:

+-------------------------------------------------------------------+
|  Line-Circle Measurement Tool                                      |
+-------------------------------------------------------------------+
|  [Basic Params]  [Measure Params]  [Result Display]               |
+-------------------------------------------------------------------+
|                                                                    |
|  Tab content area with vertical scroll support                     |
|                                                                    |
+-------------------------------------------------------------------+
|  [OK]  [Cancel]  [Apply]                                          |
+-------------------------------------------------------------------+

The window title displays "Line-Circle Measurement Tool". Below the title
is a TabControl with three tab items: Basic Parameters, Measurement
Parameters, and Result Display. The bottom action bar contains OK, Cancel,
and Apply buttons.

## 3. Tab 1: Basic Parameters

This tab configures the input sources. It has three vertical sections.

### 3.1 Layout Diagram

+-------------------------------------------------------------------+
|  Basic Parameters                                                  |
+-------------------------------------------------------------------+
|                                                                    |
|  +----------------------------------------------------------------+ |
|  |  Input Image                               [Select Image]     | |
|  +----------------------------------------------------------------+ |
|                                                                    |
|  +----------------------------------------------------------------+ |
|  |  Line Input                                                    | |
|  |                                                                  | |
|  |  (o) By Line       ( ) By Parameters                           | |
|  |                                                                  | |
|  |  Line Source: [________________]  [Link]                       | |
|  |                                                                  | |
|  |  A: [_____]  B: [_____]  C: [_____] (when By Params active)   | |
|  +----------------------------------------------------------------+ |
|                                                                    |
|  +----------------------------------------------------------------+ |
|  |  Circle Input                                                  | |
|  |                                                                  | |
|  |  (o) By Circle     ( ) By Parameters                           | |
|  |                                                                  | |
|  |  Circle Source: [________________]  [Link]                     | |
|  |                                                                  | |
|  |  X: [_____]  Y: [_____]  R: [_____] (when By Params active)   | |
|  +----------------------------------------------------------------+ |
|                                                                    |
+-------------------------------------------------------------------+

### 3.2 Input Image Section

This section contains an ImageSourceSelector component wrapped inside a
LabeledControl with the display label "Input Image".

The ImageSourceSelector allows the user to pick an existing image source
from the workflow. It includes a dropdown combo box listing all available
image sources in the current workflow sequence. A "Select Image" button
opens a selection dialog.

### 3.3 Line Input Section

This section uses two RadioButtons to choose the line input mode:

1. "By Line" (default): The user selects a line measurement result produced
   by an upstream tool in the workflow. The TextBox below displays the name
   of the currently linked line result. The "Link" button opens a tree-view
   dialog showing all available line measurement results from prior steps.

2. "By Parameters": The user enters the line equation coefficients manually.
   Three numeric fields (A, B, C) are shown for the standard line form:
   A*x + B*y + C = 0. When this mode is active, the TextBox shows a summary
   like "A=1.0, B=0.0, C=-100.0".

The RadioButton selection toggles the visibility of the corresponding
input controls. Only the active mode's controls are enabled.

### 3.4 Circle Input Section

This section uses two RadioButtons to choose the circle input mode:

1. "By Circle" (default): The user selects a circle measurement result
   produced by an upstream tool in the workflow. The TextBox shows the
   linked circle result name. The "Link" button opens a tree-view dialog
   showing available circle measurement results.

2. "By Parameters": The user enters circle parameters manually. Three
   numeric fields (CenterX, CenterY, Radius) are shown. When this mode
   is active, the TextBox shows a summary like "X=200, Y=150, R=50".

Validation: CenterX and CenterY can be any floating point value. Radius
must be greater than zero.

## 4. Tab 2: Measurement Parameters

This tab configures how the measurement is computed and reported.

### 4.1 Layout Diagram

+-------------------------------------------------------------------+
|  Measurement Parameters                                            |
+-------------------------------------------------------------------+
|                                                                    |
|  Measurement Mode:                                                 |
|                                                                    |
|  (o) Center to Line Distance                                       |
|  ( ) Shortest Distance                                             |
|                                                                    |
|  Inside/Outside:                                                   |
|                                                                    |
|  (o) Inside      ( ) Outside     ( ) Nearest                      |
|                                                                    |
|  Unit:                                                             |
|                                                                    |
|  (o) px           ( ) mm                                          |
|                                                                    |
|  Pixel Calibration:  [______1.000______]                          |
|  Decimal Places:     [______3_________]                           |
|                                                                    |
|  Tolerance:                                                        |
|                                                                    |
|  [x] Enable Tolerance                                              |
|                                                                    |
|  Upper Limit:  [______10.000_____]                                |
|  Lower Limit:  [______0.000______]                                |
|                                                                    |
+-------------------------------------------------------------------+

### 4.2 Measurement Mode

Two RadioButtons:

1. "Center to Line Distance": Computes the perpendicular distance from
   the circle center point to the line. Formula:
   distance = |A*Cx + B*Cy + C| / sqrt(A^2 + B^2)
   This reports how far the center point is from the line.

2. "Shortest Distance": Computes the shortest distance from the circle
   edge to the line. Formula:
   distance = max(0, centerDist - Radius)
   This reports the gap between the circle boundary and the line.
   Returns 0 when the circle intersects or overlaps the line.

### 4.3 Inside/Outside

Three RadioButtons:

1. "Inside": The measurement value is positive when the circle center
   lies on the inside side of the line. The sign indicates the relative
   position.

2. "Outside": The measurement value is positive when the circle center
   lies on the outside side of the line. Effectively inverts the sign
   compared to Inside mode.

3. "Nearest": Always returns the absolute distance value. No sign is
   applied regardless of which side the center lies on.

### 4.4 Unit Selection

Two RadioButtons:

1. "px" (pixels): The result is reported in image pixel units. No unit
   conversion is applied.

2. "mm" (millimeters): The result is converted using the pixel calibration
   value. Formula: distance_mm = distance_px * calibration.

### 4.5 Pixel Calibration

A NumericUpDown control labeled "Pixel Calibration". Represents the
physical size of one pixel, in mm/pixel. Default value: 1.0.
This control is enabled only when the unit is set to "mm".
Range: 0.0001 to 1000.0.

### 4.6 Decimal Places

A NumericUpDown control labeled "Decimal Places". Controls the number of
decimal digits shown in the result. Range: 0 to 6. Default: 3.
Affects both the text result display and numeric labels in the graphics
area.

### 4.7 Tolerance Settings

A GroupBox labeled "Tolerance" containing:

1. "Enable Tolerance" CheckBox: When checked, tolerance checking is active.
   The measurement value is compared against the Upper and Lower limits.

2. "Upper Limit" NumericUpDown: The maximum acceptable value. The
   measurement passes if abs(distance) is within the range.

3. "Lower Limit" NumericUpDown: The minimum acceptable value. The
   measurement passes if abs(distance) is within the range.

When tolerance is enabled and the measured value falls outside the
configured range, the result displays "FAIL" and the IsPass flag is set
to false.

## 5. Tab 3: Result Display

This tab shows computed results in graphical and textual form.

### 5.1 Layout Diagram

+-------------------------------------------------------------------+
|  Result Display                                                    |
+-------------------------------------------------------------------+
|                                                                    |
|  Judgment: [ PASS ] (green)                                        |
|                                                                    |
|  +--------------------------------------------------------------+ |
|  |                                                               | |
|  |           ///////////////                                    | |
|  |         //               //          Legend:                 | |
|  |        /                   /                                 | |
|  |       /         +          /   + = Center (red cross)        | |
|  |       |         |          |   / = Circle outline (cyan)     | |
|  |       |         |          |   --- = Line (cyan)             | |
|  |        \        |         /    | = Perpendicular (green)     | |
|  |         \       |        /                                   | |
|  |          \      |       /                                    | |
|  |           \     |      /                                     | |
|  |            \    |     /                                      | |
|  |     --------\---|----/-------                                | |
|  |              \  |   /                                       | |
|  |               \ |  /                                        | |
|  |                \| /                                         | |
|  +--------------------------------------------------------------+ |
|                                                                    |
|  Text Results:                                                     |
|  ----------------------------------------------------------------- |
|  Measurement Value:   12.345 mm                                    |
|  Circle Center:       (100.00, 200.00)                             |
|  Pixel Distance:      50.123 px                                    |
|  Mode:                Center to Line Distance                      |
|  Inside/Outside:      Inside                                       |
|  Tolerance:           Within Range                                  |
|                                                                    |
+-------------------------------------------------------------------+

### 5.2 Result Judgment

At the top of the tab, a large Label displays "PASS" in green or "FAIL"
in red. This is determined by the tolerance check. When tolerance is
disabled, the result always shows "PASS".

### 5.3 Graphical Display

The image display area renders the following visual elements:

1. **Circle Outline (Cyan)**: The circle is drawn using a cyan-colored
   outline. The line thickness is 2 pixels. The outline represents the
   full circle boundary at the specified radius.

2. **Line (Cyan)**: The reference line is drawn in cyan. It extends
   across the entire image width/height to provide visual context.

3. **Center Mark (Red Cross)**: The circle center is marked with a red
   crosshair symbol. The cross consists of two perpendicular lines,
   each 10 pixels long, crossing at the center point. The center
   coordinates are displayed as a text overlay nearby.

4. **Perpendicular Line (Green)**: When measurement mode is "Center to
   Line Distance", a green line from the circle center to the line is
   drawn perpendicular to the line. This visualizes the measured distance.
   The line is drawn with a 1-pixel width and dashed style.

The image display supports:
- Zoom in/out via mouse wheel
- Pan via click-and-drag
- Hover tooltip showing pixel coordinates and values

### 5.4 Textual Display

Below the graphics area, textual results are shown in a list format:

1. "Measurement Value": The final distance value, in the selected unit
   (px or mm). This is the primary output of the tool.

2. "Circle Center": The (X, Y) coordinates of the circle center in
   pixel units, displayed in parentheses.

3. "Pixel Distance": The raw distance in pixels before unit conversion.
   Useful for debugging and verification.

4. "Mode": Displays "Center to Line Distance" or "Shortest Distance"
   indicating which measurement mode was used.

5. "Inside/Outside": Displays "Inside", "Outside", or "Nearest"
   depending on the selected setting.

6. "Tolerance": Shows "Within Range" or "Out of Range" based on the
   tolerance comparison.

## 6. Interaction Details

1. When the user changes the unit from px to mm, the Measurement Value
   field recalculates automatically using the current Pixel Calibration.

2. Changing Decimal Places immediately updates all numeric text fields
   in both textual and graphical result areas.

3. The Link button opens a modal tree-view dialog showing all available
   measurement results from workflow steps executed before this tool.
   Line results are grouped under a "Lines" heading, circle results under
   "Circles". The user selects one and clicks OK to link.

4. The OK button persists the configuration and closes the dialog.
   Cancel discards changes. Apply persists changes without closing.

5. The Result Display tab updates in real time when the user changes
   parameters in the Basic Parameters or Measurement Parameters tabs.
