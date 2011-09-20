// 
// NSObjectProjectInfo.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (c) 2011 Novell, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MonoDevelop.Projects;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Projects.Dom.Parser;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using System.Text;

namespace MonoDevelop.MacDev.ObjCIntegration
{
	public class NSObjectProjectInfo
	{
		Dictionary<string,NSObjectTypeInfo> objcTypes = new Dictionary<string,NSObjectTypeInfo> ();
		Dictionary<string,NSObjectTypeInfo> cliTypes = new Dictionary<string,NSObjectTypeInfo> ();
		
		NSObjectInfoService infoService;
		ProjectDom dom;
		bool needsUpdating;
		
		public NSObjectProjectInfo (ProjectDom dom, NSObjectInfoService infoService)
		{
			this.infoService = infoService;
			this.dom = dom;
			needsUpdating = true;
		}
		
		internal void SetNeedsUpdating ()
		{
			needsUpdating = true;
		}
		
		internal void Update (bool force)
		{
			if (force)
				SetNeedsUpdating ();
			Update ();
		}
		
		internal void Update ()
		{
			if (!needsUpdating)
				return;
			
			foreach (var r in dom.References) {
				var info = infoService.GetProjectInfo (r);
				if (info != null)
					info.Update ();
			}
			
			objcTypes.Clear ();
			cliTypes.Clear ();
			
			foreach (var type in infoService.GetRegisteredObjects (dom)) {
				objcTypes.Add (type.ObjCName, type);
				cliTypes.Add (type.CliName, type);
			}
			
			foreach (var type in cliTypes.Values)
				ResolveCliToObjc (type);
			
			needsUpdating = false;
		}
		
		public IEnumerable<NSObjectTypeInfo> GetTypes ()
		{
			return objcTypes.Values;
		}
		
		public NSObjectTypeInfo GetType (string objcName)
		{
			NSObjectTypeInfo ret;
			if (objcTypes.TryGetValue (objcName, out ret))
				return ret;
			return null;
		}
		
		internal void InsertUpdatedType (NSObjectTypeInfo type)
		{
			objcTypes[type.ObjCName] = type;
			cliTypes[type.CliName] = type;
		}
		
		bool TryResolveCliToObjc (string cliType, out NSObjectTypeInfo resolved)
		{
			if (cliTypes.TryGetValue (cliType, out resolved))
				return true;
			foreach (var r in dom.References) {
				var rDom = infoService.GetProjectInfo (r);
				if (rDom != null && rDom.cliTypes.TryGetValue (cliType, out resolved))
					return true;
			}
			resolved = null;
			return false;
		}
		
		bool TryResolveObjcToCli (string objcType, out NSObjectTypeInfo resolved)
		{
			if (objcTypes.TryGetValue (objcType, out resolved))
				return true;
			foreach (var r in dom.References) {
				var rDom = infoService.GetProjectInfo (r);
				if (rDom != null && rDom.objcTypes.TryGetValue (objcType, out resolved))
					return true;
			}
			var msg = new StringBuilder ("Can't resolve "+ objcType + Environment.NewLine);
			foreach (var r in dom.References) {
				msg.Append ("Referenced dom:");
				msg.Append (r);
				var rDom = infoService.GetProjectInfo (r);
				if (rDom == null) {
					msg.AppendLine ("projectinfo == null");
					continue;
				}
				msg.Append ("known types:");
				msg.AppendLine (string.Join (",", rDom.objcTypes.Keys.ToArray()));
			}
			LoggingService.LogWarning (msg.ToString ());
			
			resolved = null;
			return false;
		}
		
		/// <summary>
		/// Resolves the Objective-C types by mapping the known .NET type information.
		/// </summary>
		/// <param name='type'>
		/// An NSObjectTypeInfo with the .NET type information filled in.
		/// </param>
		public void ResolveCliToObjc (NSObjectTypeInfo type)
		{
			NSObjectTypeInfo resolved;
			
			if (type.BaseObjCType == null) {
				if (TryResolveCliToObjc (type.BaseCliType, out resolved)) {
					if (resolved.IsModel)
						type.BaseIsModel = true;
					type.BaseObjCType = resolved.ObjCName;
					//FIXME: handle type references better
					if (resolved.IsUserType)
						type.UserTypeReferences.Add (resolved.ObjCName);
				} else {
					// managed classes may have implicitly registered base classes with a name not
					// expressible in obj-c. In this case, the best we can do is walk down the 
					// hierarchy until we find a valid base class
					foreach (var bt in dom.GetInheritanceTree (dom.GetType (type.BaseCliType))) {
						if (bt.ClassType != ClassType.Class)
							continue;
						
						if (TryResolveCliToObjc (bt.FullName, out resolved)) {
							if (resolved.IsModel)
								type.BaseIsModel = true;
							type.BaseObjCType = resolved.ObjCName;
							if (resolved.IsUserType)
								type.UserTypeReferences.Add (resolved.ObjCName);
							break;
						}
					}
				}
			}
			
			foreach (var outlet in type.Outlets) {
				if (outlet.ObjCType != null)
					continue;
				
				if (TryResolveCliToObjc (outlet.CliType, out resolved)) {
					outlet.ObjCType = resolved.ObjCName;
					if (resolved.IsUserType)
						type.UserTypeReferences.Add (resolved.ObjCName);
				}
			}
			
			foreach (var action in type.Actions) {
				foreach (var param in action.Parameters) {
					if (param.ObjCType != null)
						continue;
					
					if (TryResolveCliToObjc (param.CliType, out resolved)) {
						param.ObjCType = resolved.ObjCName;
						if (resolved.IsUserType)
							type.UserTypeReferences.Add (resolved.ObjCName);
					}
				}
			}
		}
		
		/// <summary>
		/// Resolves the type by mapping the known Objective-C type information to .NET types.
		/// </summary>
		/// <returns>
		/// The number of unresolved types still remaining.
		/// </returns>
		/// <param name='type'>
		/// The NSObjectTypeInfo that contains the known Objective-C type information.
		/// Typically this will be the result of NSObjectInfoService.ParseHeader().
		/// </param>
		/// <param name='force'>
		/// Force resolution of type information even if there isn't a known mapping.
		/// This will use a "best guess" approach.
		/// </param>
		public int ResolveObjcToCli (NSObjectTypeInfo type, /* DotNetProject project - we'll likely need this for context if force is true, */ bool force)
		{
			NSObjectTypeInfo resolved;
			int unresolved = 0;
			
			// Resolve our type
			if (type.CliName == null) {
				if (TryResolveObjcToCli (type.ObjCName, out resolved)) {
					type.CliName = resolved.CliName;
					type.IsModel = resolved.IsModel;
				} else if (force) {
					// FIXME: what do we do here?
				} else {
					unresolved++;
				}
			}
			
			// Resolve our base type
			if (type.BaseCliType == null) {
				if (TryResolveObjcToCli (type.BaseObjCType, out resolved)) {
					type.BaseCliType = resolved.CliName;
				} else if (force) {
					// FIXME: what do we do here?
				} else {
					unresolved++;
				}
			}
			
			// Resolve [Outlet] types
			foreach (var outlet in type.Outlets) {
				if (outlet.CliType != null)
					continue;
				
				if (TryResolveObjcToCli (outlet.ObjCType, out resolved)) {
					outlet.CliType = resolved.CliName;
				} else if (force) {
					// FIXME: maybe we can use the namespace of the ObjCType to map to a FQN? UI -> MonoTouch.UIKit.ObjCType?
					outlet.CliType = outlet.ObjCType;
				} else {
					unresolved++;
				}
			}
			
			// Resolve [Action] param types
			foreach (var action in type.Actions) {
				foreach (var param in action.Parameters) {
					if (param.CliType != null)
						continue;
					
					if (TryResolveObjcToCli (param.ObjCType, out resolved)) {
						param.CliType = resolved.CliName;
					} else if (force) {
						// FIXME: maybe we can use the namespace of the ObjCType to map to a FQN? UI -> MonoTouch.UIKit.ObjCType?
						param.CliType = param.ObjCType;
					} else {
						unresolved++;
					}
				}
			}
			
			return unresolved;
		}
	}
}