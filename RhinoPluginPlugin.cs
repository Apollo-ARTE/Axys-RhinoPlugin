using System;
using Rhino;
using Rhino.PlugIns;
using Rhino.Commands;

namespace RhinoPlugin
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class RhinoPluginPlugin : Rhino.PlugIns.PlugIn
    {
        public RhinoPluginPlugin()
        {
            Instance = this;
        }
        
        ///<summary>Gets the only instance of the RhinoPluginPlugin plug-in.</summary>
        public static RhinoPluginPlugin Instance { get; private set; }


        // protected override LoadReturnCode OnLoad(ref string errorMessage)
        // {
        //     // Register your commands here
        //     Rhino.Commands.Command.BeginCommand += OnBeginCommand;
        //     Rhino.Commands.Command.EndCommand += OnEndCommand;
            
        //     return LoadReturnCode.Success;
        // }
        // // This method is where you register your commands
        // protected override void LoadPlugIn(ref bool termination)
        // {
        //     // Register your default RhinoPluginCommand
        //     var defaultCommand = new RhinoPluginCommand();
            
        //     // Register your custom commands
        //     var webSocketCommand = new StartWebSocketServerCommand();
        //     var trackObjectCommand = new TrackObjectCommand();
            
        //     // Add both commands to Rhino command table
        //     Rhino.Commands.Command.Register(defaultCommand);
        //     Rhino.Commands.Command.Register(webSocketCommand);
        //     Rhino.Commands.Command.Register(trackObjectCommand);
        // }

        // protected override LoadReturnType Load(ref string errorMessage)
        // {
        //     // Register your custom commands here
        //     Rhino.Commands.Command.Add(new StartWebSocketServerCommand());
        //     Rhino.Commands.Command.Add(new TrackObjectCommand());

        //     return LoadReturnType.Success;
        // }

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and maintain plug-in wide options in a document.
    }
}