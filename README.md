# Dynamic Wheel Scanner for Unity
![Slices](https://github.com/user-attachments/assets/68d22611-8d8e-492c-a16e-ba9b7bc12130)


A physics-based wheel and ground scanning system developed for Unity.

## Features

* **Primary Objective:** While the ultimate goal is to achieve detailed collision detection using a fully dynamic BoxCast structure and process this data via shaders, this initial version is designed specifically to resolve the limitations of single-raycast systems.
* **Multi-Slice Scanning:** Ensures realistic ground contact by scanning not just the wheel center, but across its entire width.
* **Weighted Normal Calculation:** Smooths the ground surface normal by weighting it based on the tire's penetration depth.
* **UPM Ready:** Can be installed directly via Unity Package Manager.
