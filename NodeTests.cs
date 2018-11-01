//------------------------------------------------------------------------------
// <copyright file="LocalSandboxExecution.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TuxedoBSA;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using System.Linq;

namespace TuxedoBSA
{
    class NodeTest
    {
        [Test("Test Blessed Node Images")]
        [PersonToNotify("Patrick Lee", "patle@microsoft.com")]
        public static async Task<List<TestResult>> NodeTestsRunner()
        {
            try
            {
                String resourceGroupName = "appsvcbuildrg";
                String appServicePlanName = "appsvcbuild-plan";

                var keyVault = Utility.GetVault();
                String _clientId = keyVault["PatleNodeTestClientID"];
                String _clientSecret = keyVault["PatleNodeTestClientSecret"];
                String _tenantId = keyVault["PatleNodeTestTenantID"];
                String _subId = keyVault["PatleNodeTestSubID"];

                ServiceClientCredentials serviceCreds = Runner.getAppCredentials(_tenantId, _clientId, _clientSecret).Result;
                ResourceManagementClient resourceClient = new ResourceManagementClient(serviceCreds) { SubscriptionId = _subId };
                WebSiteManagementClient webClient = new WebSiteManagementClient(serviceCreds) { SubscriptionId = _subId };

                List<TestResult> results = new List<TestResult>();

                // Test hosting start sites
                AppServicePlan appServicePlan = webClient.AppServicePlans.Get(resourceGroupName, appServicePlanName);
                List<Site> appServicePlanApps = webClient.WebApps.ListByResourceGroup(resourceGroupName) //Get all of the apps in the resource group
                                                          .Where(x => x.ServerFarmId == appServicePlan.Id) //Get all of the apps in the given app service plan
                                                          .ToList();

                List<Site> nodeHostingStartApps =  appServicePlanApps.Where(x => x.Name.Contains("node-hostingstart")).ToList();
                foreach (Site site in nodeHostingStartApps)
                {
                    results.AddRange(await TestNodeSiteAsync(serviceCreds, _subId, resourceGroupName, site));
                }

                // Test node app sites
                List<Site> nodeApps = appServicePlanApps.Where(x => x.Name.Contains("node-app")).ToList();
                foreach (Site site in nodeApps)
                {
                    results.AddRange(await TestNodeAppAsync(site));
                }

                return results;
            }
            catch (Exception ex)
            {
                // make this better
                return new List<TestResult>() {
                    new TestResult(){
                        Name = "NodeTests",
                        Result = false,
                        Message = "Unexpected Exception: " + Utility.UnravelExceptionsToString(ex)
                }};
            }
        }

        private static async Task<List<TestResult>> TestNodeAppAsync(Site site)
        {
            List<TestResult> results = new List<TestResult>();

            String nodeVersion = site.Name.Replace("appsvcbuild-node-app-", "").Replace("-site", "").Replace("-", ".");
            String expected = String.Format("Version: v{0}\nHello World!", nodeVersion);
            var mainSiteTest = await HttpTests.GetResponseFromUrl("http://" + site.HostNames.FirstOrDefault(), expected);
            results.Add(mainSiteTest);

            return results;
        }

        private static async Task<List<TestResult>> TestNodeSiteAsync(ServiceClientCredentials serviceCreds, String _subId, String resourceGroupName, Site site)
        {
            List<TestResult> results = new List<TestResult>();

            var mainSiteTest = await HttpTests.GetResponseFromUrl("http://" + site.HostNames.FirstOrDefault());
            results.Add(mainSiteTest);

            var kuduSiteTest = await HttpTests.TestKuduForSite("https://" + site.Name + ".scm.azurewebsites.net", serviceCreds, _subId, resourceGroupName, site.Name);
            results.Add(kuduSiteTest);

            return results;
        }
    }
}
