//
// ManageProjectViewModel.cs
//
// Author:
//       Matt Ward <matt.ward@microsoft.com>
//
// Copyright (c) 2019 Microsoft
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

namespace MonoDevelop.PackageManagement
{
	class ManageProjectViewModel
	{
		public ManageProjectViewModel (ManagePackagesProjectInfo projectInfo, string packageId)
		{
			ProjectInfo = projectInfo;

			var package = ProjectInfo.Packages.FirstOrDefault (package => StringComparer.OrdinalIgnoreCase.Equals (package.Id, packageId));
			if (package != null) {
				IsChecked = true;
				PackageVersion = package.Version.ToString ();
			} else {
				PackageVersion = "–";
			}
		}

		public IDotNetProject Project {
			get { return ProjectInfo.Project; }
		}

		public string ProjectName {
			get { return Project.Name; }
		}

		public string PackageVersion { get; set; }

		public bool IsChecked { get; set; }

		internal ManagePackagesProjectInfo ProjectInfo { get; set; }
	}
}
