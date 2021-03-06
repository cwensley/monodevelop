// 
// InspectorAddinNode.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2011 Novell, Inc (http://www.novell.com)
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
using MonoDevelop.SourceEditor;
using Mono.Addins;
using MonoDevelop.Core;

namespace MonoDevelop.Inspection
{
	public class InspectorAddinNode : TypeExtensionNode
	{
		[NodeAttribute ("mimeType", Required=true, Description="The mime type of this action.")]
		string mimeType = null;
		
		public string MimeType {
			get {
				return mimeType;
			}
		}
		
		[NodeAttribute ("_title", Required=true, Localizable=true, Description="The title of this action.")]
		string title = null;
		
		public string Title {
			get {
				return title;
			}
		}
		
		[NodeAttribute ("_description", Required=true, Localizable=true,  Description="The description of this action.")]
		string description = null;
		
		public string Description {
			get {
				return description;
			}
		}
		
		[NodeAttribute ("severity", Required=true, Localizable=true,  Description="The severity of this action.")]
		QuickTaskSeverity severity;
		public QuickTaskSeverity Severity {
			get {
				return severity;
			}
		}
		
		object inspector;
		public object Inspector {
			get {
				if (inspector == null)
					inspector = CreateInstance ();
				return inspector;
			}
		}
		
		public QuickTaskSeverity GetSeverity ()
		{
			return PropertyService.Get<QuickTaskSeverity> ("refactoring.inspectors." + MimeType + "." + Type.FullName, Severity);
		}
		
		public void SetSeverity (QuickTaskSeverity severity)
		{
			PropertyService.Set ("refactoring.inspectors." + MimeType + "." + Type.FullName, severity);
		}
	}
}

