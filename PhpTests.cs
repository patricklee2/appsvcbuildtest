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
    class PhpTest
    {
        [Test("Test Blessed Php Images")]
        [PersonToNotify("Patrick Lee", "patle@microsoft.com")]
        public static async Task<List<TestResult>> PhpTestsRunner()
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

               List<Site> phpHostingStartApps = appServicePlanApps.Where(x => x.Name.Contains("php-hostingstart")).ToList();
                foreach (Site site in phpHostingStartApps)
                {
                    results.AddRange(await TestPhpSiteAsync(serviceCreds, _subId, resourceGroupName, site));
                }

                // Test php app sites
                List<Site> phpApps = appServicePlanApps.Where(x => x.Name.Contains("php-app")).ToList();
                foreach (Site site in phpApps)
                {
                    results.AddRange(await TestPhpAppAsync(site));
                }

                return results;
            }
            catch (Exception ex)
            {
                // make this better
                return new List<TestResult>() {
                    new TestResult(){
                        Name = "PhpTests",
                        Result = false,
                        Message = "Unexpected Exception: " + Utility.UnravelExceptionsToString(ex)

                }};
            }
        }

        private static async Task<List<TestResult>> TestPhpAppAsync(Site site)
        {
            List<TestResult> results = new List<TestResult>();

            String phpVersion = site.Name.Replace("appsvcbuild-php-app-", "").Replace("-site", "").Replace("-", ".");
            String expected = String.Format("<html>\n <head>\n  <title>PHP Test</title>\n </head>\n <body>\n Hello World<br/>Current PHP version: {0} \n </body>\n</html>\n", phpVersion);
            var mainSiteTest = await HttpTests.GetResponseFromUrl("http://" + site.HostNames.FirstOrDefault(), expected);
            results.Add(mainSiteTest);

            return results;
        }

        private static async Task<List<TestResult>> TestPhpSiteAsync(ServiceClientCredentials serviceCreds, String _subId, String resourceGroupName, Site site)
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
