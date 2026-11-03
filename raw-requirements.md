Task: .NET Application Development

Objective:
Design and implement a Minimum Viable Product (MVP) for a House Broker Application
using the .NET, MSSQL for the database, and adhering to Clean Architecture principles.

Requirements:
User Authentication:
● Implement user authentication using .NET Identity or a similar
authentication mechanism.
● Differentiate between house seekers and brokers in the authentication
process.

Listing Management:
● Implement CRUD operations for property listings, ensuring data integrity in
the MSSQL database.
● Include essential details such as property type, location, price, and
features.
● Expose the API so can be used by third party
● Add proper validation.
● Implement caching.

Implement a commission engine for Brokers:
Each property listing should calculate an estimated broker commission using the
following logic:
● If price < 50,00,000 → 2%
● If price between 50,00,000 and 1 crore → 1.75%
● If price > 1 crore → 1.5%
● The commission amount should be stored/displayed with the listing (but only
visible to the broker who owns the listing).
● Make commission rates configurable via the DB.

Show Case:
● Implement a search functionality for users to find properties based on
criteria such as location, price range, and property type.
● Provide filters to refine search results.
● Retrieve and display detailed information about each property from the
MSSQL database, including images, description, and contact details of the
broker.

Unit Testing:
● Write Unit tests for
○ Commission logic
○ Listing service logic
○ Basic controller tests with mocked dependencies

Submission Guidelines:
● Provide well-documented source code following Clean Architecture principles
with clear comments explaining the functionality of each layer.
● Include a README file with instructions for setting up and running the
application, including any necessary database setup.
