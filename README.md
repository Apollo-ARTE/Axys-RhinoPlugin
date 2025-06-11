# RhinoPlugin

**RhinoPlugin** is a plugin for **Rhino 8** that enables real-time communication with the **Apple Vision Pro** headset via WebSocket.
It is designed to work alongside the **Axys** companion app [Axys](https://github.com/Apollo-ARTE/Axys) (for VisionOS), allowing export and augmented reality visualization of 3D models from Rhino.

---

## Key Features

- WebSocket connection between Rhino and Apple Vision Pro
- Export of selected 3D objects from Rhino in USDZ format
- Real-time AR visualization via the **Axys** app
- Spatial calibration between model and physical environment
- Support for constrained robotic 3D printing with customizable workspace

---

## Requirements

- Rhino 8 installed
- Local network connection (same Wi-Fi) between Vision Pro and computer
- **Axys** app installed on the Vision Pro

---

## Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/Apollo-ARTE/RhinoPlugin.git
   ```

2. Build and load the plugin into Rhino following the official plugin development guidelines.

3. In Rhino, start the WebSocket server with the command:

   ```
   WebSocketStart
   ```

4. Note the displayed IP address; it must be entered into the **Axys** app.

---

## Using with the Axys App

1. Launch the Axys app on Vision Pro.
2. Enter the IP address shown by Rhino to establish the connection.
3. Follow the guided spatial calibration.
4. Select an object in Rhino.
5. Press **Export** in the Axys app to visualize it in AR.
6. Changes in Rhino are reflected in real time.

---

## Robotic Printing Integration

This plugin also supports workflows for **constrained robotic 3D printing**.  
You can map and calibrate a real workspace in Rhino, ensuring consistency between the digital model and the physical setup.

---

## Technologies Used

- **C#** with RhinoCommon SDK
- **WebSocket** server
- **USDZ** model export
- **Swift / RealityKit** for the VisionOS companion app (**Axys**)

---

## Contributing

Contributions, bug reports, and feature suggestions are welcome!

1. Fork the repository
2. Create a branch:  
   ```bash
   git checkout -b feature-name
   ```
3. Make your changes and commit
4. Submit a Pull Request

---

## License

This project is licensed under the [GNU GPL v3.0](https://www.gnu.org/licenses/gpl-3.0.html).  
You may use, modify, and redistribute it under the same license terms. See the `LICENSE` file for details.

---

## Authors
 
Rhino Plugin by [Salvatore Flauto](https://github.com/XlSolver / https://www.linkedin.com/in/salvatore-flauto-71020b190/)  
Companion app **Axys** developed for Vision Pro.
