using System;
using Rhino;
using Rhino.PlugIns;
using Rhino.Commands;

namespace Axys
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class AxysPlugin : Rhino.PlugIns.PlugIn
    {
        public AxysPlugin()
        {
            Instance = this;
        }
        
        ///<summary>Gets the only instance of the RhinoPluginPlugin plug-in.</summary>
        public static AxysPlugin Instance { get; private set; }

    }
}