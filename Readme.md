This application allows the user to submit website urls to be parsed and compares the word from the website to already categorized websites to get a distribution of percentages of what type of website category the website falls under. The percentage is based on how many of the total words from the website that is being analyzed appears on previously analyzed sites that was given that category (if a site has 10 words, and all words have appeared at least once on other sites that have been categorized as "Sport" websites, they would be given a 100% chance of being a sport website.)

To prevent timeout, a max amount of pages are parsed from a given website. This could be improved via enabling async analysis and report fetching but was beyond the limitations set on this project.

To ensure ethical parsing of websites, the Robots.txt found in the directory of the website is strictly followed. This means that some websites might not be able to be analyzed if a total block is stated in the text file. Ensure that a website you want to try to analysis has allowed generic web crawlers, otherwise the result will almost guaranteed be invalid.

It also allows a user to upload a website to the database with a category chosen by user. This will then be used as the ground for future analyses and needs to be done atleast once before a site can be analyzed.

This is performed by sending API calls via APIGateway. API calls are:

Post website to be categorized: (EXAMPLE_API_URL)/StartAnalysis
Upload a website with a given category for future analyses : (EXAMPLE_API_URL)/UploadWebsiteContent

To test this functionality, an AWS account needs to be set up with credentials with necessary access to allow lambda execution, API gateway setup and DynamoDB read/write. This so that serverless can deploy the functionality necessary to perform the code and store the categorized data.

A database has been set up with a group of sites categorized. To prevent misuse of this functionality, access is given manually to interested parties. Message me here or via ludvig.svee@gmail.com for access to the test environment.

