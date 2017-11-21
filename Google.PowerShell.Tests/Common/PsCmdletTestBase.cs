﻿// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.PowerShell.Common;
using NUnit.Framework;
using System.Management.Automation.Runspaces;

namespace Google.PowerShell.Tests.Common
{
    /// <summary>
    /// Abstract base class for running unit tests on PSCmdlets.
    /// </summary>
    [TestFixture]
    public abstract class PsCmdletTestBase
    {
        protected const string FakeRegionName = "fake-region";
        protected const string FakeZoneName = "fake-zone";
        protected const string FakeProjectName = "fake-project";
        private static readonly string s_fakeConfigJson = $@"{{
            'configuration': {{
                'active_configuration': 'testing',
                'properties': {{
                    'compute': {{
                        'region': '{FakeRegionName}',
                        'zone': '{FakeZoneName}'
                    }},
                    'core': {{
                        'account': 'testing@google.com',
                        'disable_usage_reporting': 'False',
                        'project': '{FakeProjectName}'
                    }}
                }}
            }},
            'credential': {{
                'access_token': 'fake-token',
                'token_expiry': '2012-12-12T12:12:12Z'
            }},
            'sentinels': {{
                'config_sentinel': 'sentinel.sentinel'
            }}
        }}";

        protected readonly RunspaceConfiguration Config = RunspaceConfiguration.Create();
        protected Pipeline Pipeline;

        [SetUp]
        public void BeforeEach()
        {
            ActiveUserConfig.ActiveConfig = new ActiveUserConfig(s_fakeConfigJson);
            Runspace rs = RunspaceFactory.CreateRunspace(Config);
            rs.Open();
            Pipeline = rs.CreatePipeline();
        }

        [TearDown]
        public void AfterEach()
        {
            Pipeline.Dispose();
            Pipeline.Runspace.Dispose();
        }
    }
}