using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AllianceTestPlan
{
    /// <summary>
    /// Attribute to assign priority to test methods.
    /// Lower numbers indicate higher priority (executed first).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TestPriorityAttribute : Attribute
    {
        public int Priority { get; private set; }

        public TestPriorityAttribute(int priority)
        {
            Priority = priority;
        }
    }

    /// <summary>
    /// Custom test case orderer that orders tests based on the TestPriorityAttribute.
    /// </summary>
    public class PriorityOrderer : ITestCaseOrderer
    {
        public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
        {
            var sortedCases = testCases.Select(tc =>
            {
                int priority = 0;

                // Retrieve the TestPriorityAttribute if it exists
                foreach (var attr in tc.TestMethod.Method.GetCustomAttributes((typeof(TestPriorityAttribute).AssemblyQualifiedName)))
                {
                    priority = attr.GetNamedArgument<int>("Priority");
                    break; // Assume only one TestPriorityAttribute per method
                }

                return new { TestCase = tc, Priority = priority };
            })
            .OrderBy(tc => tc.Priority)
            .Select(tc => tc.TestCase);

            return sortedCases;
        }
    }

    /// <summary>
    /// Fixture class to initialize WebDriver, perform login once, and share across tests.
    /// </summary>
    public class SpendWiseFixture : IDisposable
    {
        public IWebDriver Driver { get; private set; }
        public WebDriverWait Wait { get; private set; }

        public SpendWiseFixture()
        {
            // Initialize the ChromeDriver
            Driver = new ChromeDriver();
            Driver.Manage().Window.Maximize();

            // Initialize WebDriverWait with a 30-second timeout
            Wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));

            // Navigate to the login URL
            Driver.Navigate().GoToUrl("https://localhost:51302"); // Replace with your actual login URL

            // Perform login once
            Login("le", "123456");

            // Verify successful login by checking for the Dashboard element
            var welcomeMessage = Wait.Until(driver =>
            {
                try
                {
                    var element = driver.FindElement(By.XPath("/html/body/div/main/div/div/div[1]/div[1]/div[1]/h2"));
                    return (element.Displayed && element.Enabled) ? element : null;
                }
                catch (NoSuchElementException)
                {
                    return null;
                }
            });

            if (welcomeMessage == null || welcomeMessage.Text != "Dashboard")
            {
                throw new Exception("Login failed or Dashboard not displayed.");
            }
        }

        private void Login(string username, string password)
        {
            try
            {
                // Enter username
                var usernameInput = Wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div[2]/div/form/div[1]/input"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                usernameInput?.SendKeys(username);

                // Enter password
                var passwordInput = Wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div[2]/div/form/div[2]/div/input"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                passwordInput?.SendKeys(password);

                // Click login button
                var loginButton = Wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div[2]/div/form/button"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                loginButton?.Click();
            }
            catch (WebDriverTimeoutException ex)
            {
                throw new Exception("Login elements not found within the timeout period.", ex);
            }
        }

        public void Dispose()
        {
            // Clean up and close the browser
            if (Driver != null)
            {
                Driver.Quit();
                Driver.Dispose();
            }
        }
    }

    /// <summary>
    /// Collection definition to disable parallelization.
    /// </summary>
    [CollectionDefinition("Sequential Collection", DisableParallelization = true)]
    public class SequentialCollection : ICollectionFixture<SpendWiseFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and the ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// Test class that uses the SpendWiseFixture for shared setup.
    /// Decorated with the custom test case orderer.
    /// </summary>
    [Collection("Sequential Collection")]
    [TestCaseOrderer("AllianceTestPlan.PriorityOrderer", "AllianceTestPlan")]
    public class SpendWiseTests : IDisposable
    {
        private readonly IWebDriver _driver;
        private readonly WebDriverWait _wait;
        private readonly SpendWiseFixture _fixture;
        private readonly ITestOutputHelper _output;

        public SpendWiseTests(SpendWiseFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _driver = _fixture.Driver;
            _wait = _fixture.Wait;
            _output = output;
        }

        /// <summary>
        /// Verifies that the user is successfully logged in by checking the Dashboard.
        /// </summary>
        [Fact, TestPriority(1)]
        public void SuccessfulLoginTest()
        {
            try
            {
                // Wait up to 10 seconds for the "Dashboard" element to be visible
                var welcomeMessage = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div/div[1]/div[1]/div[1]/h2"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });

                Assert.NotNull(welcomeMessage);
                Assert.True(welcomeMessage.Displayed, "Welcome message is not displayed.");
                Assert.Equal("Dashboard", welcomeMessage.Text);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"SuccessfulLoginTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Navigates to the Category page and verifies the page header.
        /// </summary>
        [Fact, TestPriority(2)]
        public void NavigateToCategoryPageTest()
        {
            try
            {
                NavigateToCategoryPage();

                var categoryPageHeader = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/h2"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });

                Assert.NotNull(categoryPageHeader);
                Assert.True(categoryPageHeader.Displayed, "Category page header is not displayed.");
                Assert.Equal("Category", categoryPageHeader.Text); // Adjust expected text as needed
            }
            catch (Exception ex)
            {
                _output.WriteLine($"NavigateToCategoryPageTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Adds a new category and verifies its addition.
        /// </summary>
        [Fact, TestPriority(3)]
        public void AddCategoryTest()
        {
            try
            {
                NavigateToCategoryPage();
                AddCategory("Jolibee", "Cash", "#008000");

                // Define a new WebDriverWait with a 10-second timeout for validation
                var validationWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

                // Wait until the category "Jolibee" appears in the page source
                bool categoryAdded = validationWait.Until(driver =>
                    driver.PageSource.Contains("Jolibee")
                );

                // Validate the category was added
                Assert.True(categoryAdded, "The category 'Jolibee' was not found on the page.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"AddCategoryTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Edits an existing category and verifies the changes.
        /// </summary>
        [Fact, TestPriority(4)]
        public void EditCategoryTest()
        {
            try
            {
                // Navigate to the Category page
                NavigateToCategoryPage();

                // Validate if the category "Jolibee" is present before editing
                var categoryAdded = _wait.Until(driver =>
                    driver.PageSource.Contains("Jolibee")
                );
                Assert.True(categoryAdded, "The category 'Jolibee' was not found on the page and cannot be edited.");

                // Click the edit link/button for the added category
                try
                {
                    var editLink = _wait.Until(driver =>
                    {
                        try
                        {
                            // Locate the edit link/button using the provided XPath
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div[2]/div[5]/a"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    editLink?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Edit link/button was not found or not clickable within the timeout period.", ex);
                }

                // Change the category name to "Mcdo"
                try
                {
                    var nameInput = _wait.Until(driver =>
                    {
                        try
                        {
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div[3]/div/form/div[2]/input"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    nameInput?.Clear();
                    nameInput?.SendKeys("Mcdo");
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Name input field was not found or not interactable within the timeout period.", ex);
                }

                // Change the icon to "Pizza"
                try
                {
                    var iconSelect = _wait.Until(driver =>
                    {
                        try
                        {
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div[3]/div/form/div[3]/select"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    iconSelect?.Click();

                    var pizzaOption = _wait.Until(driver =>
                    {
                        try
                        {
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div[3]/div/form/div[3]/select/option[5]"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    pizzaOption?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Icon select or Pizza option was not found or not clickable within the timeout period.", ex);
                }

                // Change the color to yellow
                try
                {
                    var colorInput = _wait.Until(driver =>
                    {
                        try
                        {
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div[3]/div/form/div[4]/input"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    colorInput?.Clear();
                    colorInput?.SendKeys("#FFFF00"); // Hex code for yellow
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Color input field was not found or not interactable within the timeout period.", ex);
                }

                // Click the save button
                try
                {
                    var saveButton = _wait.Until(driver =>
                    {
                        try
                        {
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div[3]/div/form/div[6]/button[2]"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    saveButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Save button was not found or not clickable within the timeout period.", ex);
                }

                // Handle SweetAlert confirmation
                ClickSweetAlertOkButtons(1);

                // Validate that the category name has been updated to "Mcdo"
                var updatedCategoryPresent = _wait.Until(driver =>
                    driver.PageSource.Contains("Mcdo")
                );
                Assert.True(updatedCategoryPresent, "The category name was not updated to 'Mcdo'.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"EditCategoryTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deletes an existing category and verifies its removal.
        /// </summary>
        [Fact, TestPriority(5)]
        public void DeleteCategoryTest()
        {
            try
            {
                // Navigate to the Category page
                NavigateToCategoryPage();

                // Validate if the category "Mcdo" is present before deletion
                var categoryPresent = _wait.Until(driver =>
                    driver.PageSource.Contains("Mcdo")
                );
                Assert.True(categoryPresent, "The category 'Mcdo' was not found on the page and cannot be deleted.");

                // Click the edit link/button for the "Mcdo" category
                try
                {
                    var editLink = _wait.Until(driver =>
                    {
                        try
                        {
                            // Locate the edit link/button using the provided XPath
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div[2]/div[5]/a"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    editLink?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Edit link/button was not found or not clickable within the timeout period.", ex);
                }

                // Click the delete button
                try
                {
                    var deleteButton = _wait.Until(driver =>
                    {
                        try
                        {
                            // Locate the delete button using the provided XPath
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div[3]/div/form/div[6]/button[1]"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    deleteButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Delete button was not found or not clickable within the timeout period.", ex);
                }

                // Click OK on both SweetAlert dialogs
                ClickSweetAlertOkButtons(2);

                // Validate that the category "Mcdo" has been deleted
                var categoryDeleted = _wait.Until(driver =>
                    !driver.PageSource.Contains("Mcdo")
                );
                Assert.True(categoryDeleted, "The category 'Mcdo' was still found on the page after deletion.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"DeleteCategoryTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Adds a new wallet named "GCASH" with specified details.
        /// </summary>
        [Fact, TestPriority(6)]
        public void AddWalletTest()
        {
            try
            {
                // Navigate to the Wallet page
                NavigateToWalletPage();

                // Validate that the Wallet Page is displayed by checking the header
                var walletPageHeader = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div[1]/div[1]/h2"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });

                Assert.NotNull(walletPageHeader);
                Assert.True(walletPageHeader.Displayed, "Wallet page header is not displayed.");
                // Optionally, validate the header text if known
                // Assert.Equal("Wallet Page Header Text", walletPageHeader.Text);

                // Click the "Add Wallet" button
                try
                {
                    var addWalletButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[1]/div[3]/div/div[1]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    addWalletButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Add Wallet button was not found or not clickable within the timeout period.", ex);
                }

                // Enter the wallet name "GCASH"
                try
                {
                    var walletNameInput = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[2]/div/form/div/div[1]/div[1]/input"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    walletNameInput?.Click();
                    walletNameInput?.SendKeys("GCASH");
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Wallet name input field was not found or not interactable within the timeout period.", ex);
                }

                // Enter the amount "10000"
                try
                {
                    var walletAmountInput = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[2]/div/form/div/div[1]/div[2]/input"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    walletAmountInput?.Clear();
                    walletAmountInput?.SendKeys("10000");
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Wallet amount input field was not found or not interactable within the timeout period.", ex);
                }

                // Set the icon by selecting the "wallet" option
                try
                {
                    var iconSelect = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[2]/div/form/div/div[1]/div[3]/select"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    iconSelect?.Click();

                    var walletOption = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[2]/div/form/div/div[1]/div[3]/select/option[4]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    walletOption?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Icon select or Wallet option was not found or not clickable within the timeout period.", ex);
                }

                // Set the color to blue
                try
                {
                    var colorInput = _wait.Until(driver =>
                    {
                        try
                        {
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div/div[2]/div/form/div/div[1]/div[4]/input"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    colorInput?.Click();
                    colorInput?.Clear();
                    colorInput?.SendKeys("#0000FF"); // Hex code for blue
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Color input field was not found or not interactable within the timeout period.", ex);
                }

                // Click the confirm button to add the wallet
                try
                {
                    var confirmButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[2]/div/form/div/div[2]/button[2]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    confirmButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Confirm button was not found or not clickable within the timeout period.", ex);
                }

                // Handle SweetAlert confirmation by clicking the OK button
                try
                {
                    var sweetAlertOkButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div[2]/div/div[6]/button[1]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    sweetAlertOkButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("SweetAlert OK button was not found or not clickable within the timeout period.", ex);
                }

                // Validate that the wallet "GCASH" has been added successfully
                var walletAddedValidation = _wait.Until(driver =>
                    driver.FindElement(By.XPath("/html/body/div/main/div/div[1]/div[3]/div/div[2]/div[3]/h3"))
                );
                Assert.NotNull(walletAddedValidation);
                Assert.True(walletAddedValidation.Displayed, "The wallet 'GCASH' was not found on the Wallet page.");
                // Optionally, validate the wallet name text
                // Assert.Equal("GCASH", walletAddedValidation.Text);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"AddWalletTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Edits an existing wallet named "GCASH" to "GoTyme" with updated amount and color.
        /// </summary>
        [Fact, TestPriority(7)]
        public void EditWalletTest()
        {
            try
            {
                // Navigate to the Wallet page
                NavigateToWalletPage();

                // Click the "Edit" button to open the edit modal for "GCASH"
                try
                {
                    var editWalletButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[1]/div[3]/div/div[2]/button"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    editWalletButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Edit Wallet button was not found or not clickable within the timeout period.", ex);
                }

                // Change the wallet name to "GoTyme"
                try
                {
                    var walletNameInput = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[3]/div/form/div[1]/input"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    walletNameInput?.Clear();
                    walletNameInput?.SendKeys("GoTyme");
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Wallet name input field was not found or not interactable within the timeout period.", ex);
                }

                // Change the amount to "15000"
                try
                {
                    var walletAmountInput = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[3]/div/form/div[2]/input"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    walletAmountInput?.Clear();
                    walletAmountInput?.SendKeys("15000");
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Wallet amount input field was not found or not interactable within the timeout period.", ex);
                }

                // Change the color to bluegreen
                try
                {
                    var colorInput = _wait.Until(driver =>
                    {
                        try
                        {
                            var element = driver.FindElement(By.XPath("/html/body/div/main/div/div[3]/div/form/div[4]/input"));
                            return (element.Displayed && element.Enabled) ? element : null;
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    colorInput?.Click();
                    colorInput?.Clear();
                    colorInput?.SendKeys("#00FF7F"); // Hex code for bluegreen (Spring Green)
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Color input field was not found or not interactable within the timeout period.", ex);
                }

                // Click the save button to apply changes
                try
                {
                    var saveButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div/div[3]/div/form/div[5]/button[2]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    saveButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Save button was not found or not clickable within the timeout period.", ex);
                }

                // Handle SweetAlert confirmations by clicking OK twice
                ClickSweetAlertOkButtons(2);

                // Validate that the wallet name has been updated to "GoTyme"
                var walletUpdatedValidation = _wait.Until(driver =>
                    driver.FindElement(By.XPath("/html/body/div/main/div/div[1]/div[3]/div/div[2]/div[3]/h3"))
                );
                Assert.NotNull(walletUpdatedValidation);
                Assert.True(walletUpdatedValidation.Displayed, "The wallet 'GoTyme' was not found on the Wallet page.");
                // Optionally, validate the wallet name text
                // Assert.Equal("GoTyme", walletUpdatedValidation.Text);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"EditWalletTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Adds a new transaction and verifies its addition.
        /// </summary>
        [Fact, TestPriority(8)]
        public void AddTransactionTest()
        {
            try
            {
                // Navigate to the Transactions page
                NavigateToTransactionPage();

                // Validate the presence of the Transactions header
                var transactionsHeader = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div[1]/h2"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                Assert.NotNull(transactionsHeader);
                Assert.True(transactionsHeader.Displayed, "Transactions header is not displayed.");
                // Optionally, validate the header text if known
                // Assert.Equal("Transactions", transactionsHeader.Text);

                // Click the "Add Transaction" button
                try
                {
                    var addTransactionButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div[2]/div[1]/button"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    addTransactionButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Add Transaction button was not found or not clickable within the timeout period.", ex);
                }

                // Click the category dropdown
                try
                {
                    var categorySelect = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[2]/div[1]/select"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    categorySelect?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Category select dropdown was not found or not clickable within the timeout period.", ex);
                }

                // Choose "Default Expense" from the category dropdown
                try
                {
                    var defaultExpenseOption = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[2]/div[1]/select/option[3]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    defaultExpenseOption?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Default Expense option was not found or not clickable within the timeout period.", ex);
                }

                // Click the wallet dropdown
                try
                {
                    var walletSelect = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[2]/div[2]/select"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    walletSelect?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Wallet select dropdown was not found or not clickable within the timeout period.", ex);
                }

                // Choose the desired wallet (e.g., option[2])
                try
                {
                    var selectedWalletOption = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[2]/div[2]/select/option[2]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    selectedWalletOption?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Selected wallet option was not found or not clickable within the timeout period.", ex);
                }

                // Set the Amount to 500
                try
                {
                    var amountInput = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[3]/div/input"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    amountInput?.Clear();
                    amountInput?.SendKeys("500");
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Amount input field was not found or not interactable within the timeout period.", ex);
                }

                // Click the Confirm button to add the transaction
                try
                {
                    var confirmButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[6]/button[2]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    confirmButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Confirm button was not found or not clickable within the timeout period.", ex);
                }

                // Handle SweetAlert confirmations by clicking OK once
                ClickSweetAlertOkButtons(1); // As per user instruction, only once here

                // Validate that the transaction has been added by checking for the amount in the page source
                var transactionAdded = _wait.Until(driver =>
                    driver.PageSource.Contains("500")
                );
                Assert.True(transactionAdded, "The transaction with amount '500' was not found on the page.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"AddTransactionTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Edits the first transaction in the Transactions table and verifies the update.
        /// </summary>
        [Fact, TestPriority(9)]
        public void EditTransactionTest()
        {
            try
            {
                // Navigate to the Transactions page
                NavigateToTransactionPage();

                // Validate the presence of the Transactions header
                var transactionsHeader = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div[1]/h2"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                Assert.NotNull(transactionsHeader);
                Assert.True(transactionsHeader.Displayed, "Transactions header is not displayed.");

                // Click the Edit button for the first transaction
                try
                {
                    var editButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div[2]/div[2]/div[1]/table/tbody/tr[1]/td[6]/div/button[1]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    editButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Edit button for the first transaction was not found or not clickable within the timeout period.", ex);
                }

                // Change the category by selecting the fourth option using SelectElement
                try
                {
                    // Locate the select element
                    var categorySelectElement = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[3]/div/form/div[2]/div[1]/select"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });

                    // Initialize SelectElement with the located select element
                    var select = new SelectElement(categorySelectElement);

                    // Select the fourth option by index (0-based index)
                    select.SelectByIndex(3); // Index 3 corresponds to the fourth option
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Category select dropdown was not found within the timeout period.", ex);
                }
                catch (NoSuchElementException ex)
                {
                    throw new Exception("The fourth option in the category dropdown was not found.", ex);
                }

                // Change the amount to 1500
                try
                {
                    var amountInput = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[3]/div/form/div[3]/div/input"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    amountInput?.Clear();
                    amountInput?.SendKeys("1500");
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Amount input field was not found or not interactable within the timeout period.", ex);
                }

                // Click the Confirm button to save the changes
                try
                {
                    var confirmButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[3]/div/form/div[6]/button[2]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    confirmButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Confirm button was not found or not clickable within the timeout period.", ex);
                }

                // Handle SweetAlert confirmation by clicking the OK button once
                ClickSweetAlertOkButtons(1);

                // Validate that the transaction amount has been updated to "1500"
                var transactionUpdated = _wait.Until(driver =>
                {
                    try
                    {
                        // Assuming the amount is in the first column (td[1]) of the first row
                        var updatedAmountElement = driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div[2]/div[2]/div[1]/table/tbody/tr[1]/td[2]/div/div"));
                        return updatedAmountElement.Text.Contains("-₱ 1,500.00");
                    }
                    catch (NoSuchElementException)
                    {
                        return false;
                    }
                });
                Assert.True(transactionUpdated, "The transaction amount was not updated to '1500'.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"EditTransactionTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deletes the first transaction in the Transactions table and verifies its removal.
        /// </summary>
        [Fact, TestPriority(10)]
        public void DeleteTransactionTest()
        {
            try
            {
                // Navigate to the Transactions page
                NavigateToTransactionPage();

                // Validate the presence of the Transactions header
                var transactionsHeader = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div[1]/h2"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                Assert.NotNull(transactionsHeader);
                Assert.True(transactionsHeader.Displayed, "Transactions header is not displayed.");

                // Click the Delete button for the first transaction
                try
                {
                    var deleteButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div[2]/div[2]/div[1]/table/tbody/tr[1]/td[6]/div/button[2]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    deleteButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Delete button for the first transaction was not found or not clickable within the timeout period.", ex);
                }

                // Handle SweetAlert confirmations by clicking OK twice
                ClickSweetAlertOkButtons(2);

                // Validate that the transaction has been deleted
                var transactionDeleted = _wait.Until(driver =>
                    !driver.PageSource.Contains("-₱ 1,500.00")
                );
                Assert.True(transactionDeleted, "The transaction with amount '1500' was still found on the page after deletion.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"DeleteTransactionTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Performs advanced budget operations: navigating home, adding a budget, editing it, and handling confirmations.
        /// </summary>
        [Fact, TestPriority(11)]
        public void BudgetTest()
        {
            try
            {
                // Step 1: Navigate to Home Page
                NavigateToHomePage();

                // Step 2: Click the specified element
                try
                {
                    var firstElement = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div/div[2]/div[1]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    firstElement?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("First element in Home page was not found or not clickable within the timeout period.", ex);
                }

                // Step 3: Click the select dropdown
                try
                {
                    var selectDropdown = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div/div[2]/div[3]/div/form/div[1]/select"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    selectDropdown?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Select dropdown was not found or not clickable within the timeout period.", ex);
                }

                // Step 4: Choose the second option from the dropdown
                try
                {
                    var secondOption = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div/div[2]/div[3]/div/form/div[1]/select/option[2]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    secondOption?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Second option in the select dropdown was not found or not clickable within the timeout period.", ex);
                }

                // Step 5: Set the amount to 500
                try
                {
                    var amountInput = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div/div[2]/div[3]/div/form/div[2]/div/input"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    amountInput?.Clear();
                    amountInput?.SendKeys("500");
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Amount input field was not found or not interactable within the timeout period.", ex);
                }

                // Step 6: Click the confirm button
                try
                {
                    var confirmButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div/div[2]/div[3]/div/form/div[3]/button[2]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    confirmButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Confirm button was not found or not clickable within the timeout period.", ex);
                }

                // Step 7: Click SweetAlert OK button once
                ClickSweetAlertOkButtons(1);

                // Step 8: Click the edit button
                try
                {
                    var editButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[1]/div/div/div[2]/div[2]/div/div[1]/div[3]/button"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    editButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Edit button was not found or not clickable within the timeout period.", ex);
                }

                // Step 9: Click the select dropdown in edit form
                try
                {
                    var editSelectDropdown = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[1]/select"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    editSelectDropdown?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Edit select dropdown was not found or not clickable within the timeout period.", ex);
                }

                // Step 10: Choose the third option from the dropdown
                try
                {
                    var thirdOption = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[1]/select/option[3]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    thirdOption?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Third option in the edit select dropdown was not found or not clickable within the timeout period.", ex);
                }

                // Step 11: Edit the amount to 1000
                try
                {
                    var editAmountInput = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[2]/div/input"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    editAmountInput?.Clear();
                    editAmountInput?.SendKeys("1000");
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Edit amount input field was not found or not interactable within the timeout period.", ex);
                }

                // Step 12: Save the edited transaction
                try
                {
                    var saveButton = _wait.Until(driver =>
                    {
                        try
                        {
                            return driver.FindElement(By.XPath("/html/body/div/main/div[2]/div/form/div[3]/button[2]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    saveButton?.Click();
                }
                catch (WebDriverTimeoutException ex)
                {
                    throw new Exception("Save button was not found or not clickable within the timeout period.", ex);
                }

                // Step 13: Click SweetAlert OK button once
                ClickSweetAlertOkButtons(1);

                // Step 14: Validate that the transaction has been edited successfully
                var transactionEdited = _wait.Until(driver =>
                    driver.PageSource.Contains("1000")
                );
                Assert.True(transactionEdited, "The transaction amount was not updated to '1000'.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"BudgetTest failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Navigates to the Category page.
        /// </summary>
        private void NavigateToCategoryPage()
        {
            try
            {
                // Locate and click the Category link using the provided XPath
                var categoryLink = _wait.Until(driver =>
                {
                    try
                    {
                        return driver.FindElement(By.XPath("/html/body/div/nav[1]/div/nav/ul/li[3]/a"));
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                categoryLink?.Click();
            }
            catch (WebDriverTimeoutException ex)
            {
                throw new Exception("Category link was not found or not clickable within the timeout period.", ex);
            }
        }

        /// <summary>
        /// Navigates to the Wallet page.
        /// </summary>
        private void NavigateToWalletPage()
        {
            try
            {
                // Locate and click the Wallet link using the provided XPath
                var walletLink = _wait.Until(driver =>
                {
                    try
                    {
                        return driver.FindElement(By.XPath("/html/body/div/nav[1]/div/nav/ul/li[2]/a"));
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                walletLink?.Click();
            }
            catch (WebDriverTimeoutException ex)
            {
                throw new Exception("Wallet link was not found or not clickable within the timeout period.", ex);
            }
        }

        /// <summary>
        /// Navigates to the Transactions page.
        /// </summary>
        private void NavigateToTransactionPage()
        {
            try
            {
                // Locate and click the Transactions link using the provided XPath
                var transactionLink = _wait.Until(driver =>
                {
                    try
                    {
                        return driver.FindElement(By.XPath("/html/body/div/nav[1]/div/nav/ul/li[4]/a"));
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                transactionLink?.Click();
            }
            catch (WebDriverTimeoutException ex)
            {
                throw new Exception("Transactions link was not found or not clickable within the timeout period.", ex);
            }
        }

        /// <summary>
        /// Navigates to the Home page.
        /// </summary>
        private void NavigateToHomePage()
        {
            try
            {
                // Locate and click the Home link using the provided XPath
                var homeLink = _wait.Until(driver =>
                {
                    try
                    {
                        return driver.FindElement(By.XPath("/html/body/div/nav[1]/div/nav/ul/li[1]/a"));
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                homeLink?.Click();
            }
            catch (WebDriverTimeoutException ex)
            {
                throw new Exception("Home link was not found or not clickable within the timeout period.", ex);
            }
        }

        /// <summary>
        /// Adds a new category with the specified details.
        /// </summary>
        /// <param name="name">Name of the category.</param>
        /// <param name="iconName">Name of the icon.</param>
        /// <param name="color">Color code for the category.</param>
        private void AddCategory(string name, string iconName, string color)
        {
            try
            {
                // Open the Add Category modal
                var addCategoryModal = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div/div[1]/div"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                addCategoryModal?.Click();

                // Select Expense type (assuming "Expense" is the second button)
                var expenseButton = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div/form/div[1]/div/button[2]"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                expenseButton?.Click();

                // Enter the category name
                var nameInput = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div/form/div[2]/input"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                nameInput?.SendKeys(name);

                // Select the icon
                var iconDropdown = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div/form/div[3]/select"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                iconDropdown?.Click();

                // Click the specified icon option
                var iconOption = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div/form/div[3]/select/option[5]"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                iconOption?.Click();

                // Select the color
                var colorPicker = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div/form/div[4]/input"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                colorPicker?.SendKeys(color);

                // Confirm to add category
                var confirmButton = _wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(By.XPath("/html/body/div/main/div/div/form/div[6]/button[2]"));
                        return (element.Displayed && element.Enabled) ? element : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                });
                confirmButton?.Click();

                // Handle SweetAlert confirmation by clicking the OK button
                ClickSweetAlertOkButtons(1);
            }
            catch (WebDriverTimeoutException ex)
            {
                throw new Exception("An element in the Add Category process was not found or not clickable within the timeout period.", ex);
            }
        }

        /// <summary>
        /// Clicks the SweetAlert OK button a specified number of times.
        /// </summary>
        /// <param name="count">Number of Swal dialogs to handle.</param>
        private void ClickSweetAlertOkButtons(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var sweetAlertOkButton = _wait.Until(driver =>
                    {
                        try
                        {
                            // Locate the SweetAlert OK button using the provided XPath
                            return driver.FindElement(By.XPath("/html/body/div[2]/div/div[6]/button[1]"));
                        }
                        catch (NoSuchElementException)
                        {
                            return null;
                        }
                    });
                    sweetAlertOkButton?.Click();

                    // Optional: Add a small wait to allow the next Swal to appear
                    Thread.Sleep(500);
                }
                catch (WebDriverTimeoutException)
                {
                    throw new Exception($"SweetAlert OK button number {i + 1} was not found or not clickable within the timeout period.");
                }
            }
        }

        /// <summary>
        /// Dispose method to clean up resources if needed.
        /// </summary>
        public void Dispose()
        {
            // No action needed here since the fixture handles disposal
        }
    }
}
