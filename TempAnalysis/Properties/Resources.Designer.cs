﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TempAnalysis.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("TempAnalysis.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to set nodes [getNodeTags];
        ///set ndf_max 1;
        ///set total_mass {0.0 0.0 0.0};
        ///set mass_labels {&quot;MX&quot;};
        ///set mass_labels1 {&quot;MODE&quot; &quot;MX&quot;};
        ///foreach node $nodes {
        ///		set indf [llength [nodeDisp $node]]
        ///		for {set i 0} {$i &lt; $indf} {incr i} {
        ///			set imass [nodeMass $node [expr $i+1]]
        ///			set imass_total [lindex $total_mass $i]
        ///			lset total_mass $i [expr $imass_total + $imass]
        ///		}
        ///	}
        ///set pi [expr acos(-1.0)];
        ///set lambdas [eigen -fullGenLapack $num_modes];
        ///record;
        ///set mode_data [lrepeat $num_modes [lrepeat 4 0 [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ModalAnalysisScript {
            get {
                return ResourceManager.GetString("ModalAnalysisScript", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to set nodes [getNodeTags];
        ///set ndf_max 3;
        ///set total_mass {0.0 0.0 0.0};
        ///set mass_labels {&quot;MX&quot; &quot;MRZ&quot; &quot;MY&quot;};
        ///set mass_labels1 {&quot;MODE&quot; &quot;MX&quot; &quot;MRZ&quot; &quot;MY&quot;};
        ///foreach node $nodes {
        ///		set indf [llength [nodeDisp $node]]
        ///		for {set i 0} {$i &lt; $indf} {incr i} {
        ///			set imass [nodeMass $node [expr $i+1]]
        ///			set imass_total [lindex $total_mass $i]
        ///			lset total_mass $i [expr $imass_total + $imass]
        ///		}
        ///	}
        ///set pi [expr acos(-1.0)];
        ///set lambdas [eigen $num_modes];
        ///record;
        ///set mode_data [lrepeat $num_modes [lrep [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ModalAnalysisScript3D {
            get {
                return ResourceManager.GetString("ModalAnalysisScript3D", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to # ---------------------------------- in case of convergence problems
        ///if {$ok != 0} {      
        ///# change some analysis parameters to achieve convergence
        ///# performance is slower inside this loop
        ///	set ok 0;
        ///	set controlDisp 0.0;		# start from zero
        ///	set D0 0.0;		# start from zero
        ///	set Dstep [expr ($controlDisp-$D0)/($Dmax-$D0)]
        ///	set Tol 1.0e-9;
        ///	set maxNumIter 100;
        ///	set TestType EnergyIncr;
        ///	set algorithmType Newton;
        ///	while {$Dstep &lt; 1.0 &amp;&amp; $ok == 0} {	
        ///		set controlDisp [nodeDisp $IDctrlNode $IDctrlDO [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string RunMethods {
            get {
                return ResourceManager.GetString("RunMethods", resourceCulture);
            }
        }
    }
}
