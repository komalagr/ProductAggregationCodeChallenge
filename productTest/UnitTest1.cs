using productApp;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
namespace productTest
{
    public class Tests
    {

        private List<Product> products = null!;
        private List<EnrichedProduct> enrichedProducts = null!;
        private Dictionary<string, List<EnrichedProduct>> groupedProducts = null!;
        private string outputJson = null!;
        [SetUp]
        public void Setup()
        {
            var jsonFile = File.ReadAllText(@"..\..\..\testData\productsData.json");
            products = JsonConvert.DeserializeObject<List<Product>>(jsonFile)!;
            enrichedProducts = Program.EnrichProducts(products);
            groupedProducts = Program.GroupAndSortProducts(enrichedProducts);
            outputJson = JsonConvert.SerializeObject(groupedProducts, Formatting.Indented);
        }

        [Test]
        public void Test_EnrichProducts()
        {

            if (products == null || products.Count == 0)
            {
                // Handle the case when deserialization fails
                Assert.Fail("Failed to deserialize product list from JSON.");
                return;
            }
        
            Assert.That(enrichedProducts.Count, Is.EqualTo(products.Count), "The number of enriched products should match the original product list.");

            foreach (var enriched in enrichedProducts)
            {
                var expectedMin = Math.Round(enriched.original_price * 0.80, 2); // 20% discount
                var expectedMax = Math.Round(enriched.original_price * 0.95, 2); // 5% discount
                // Check if the discounted price is within the expected range
                Assert.That(enriched.discounted_price, Is.InRange(expectedMin, expectedMax), $"Discount for product {enriched.id} is out of expected range.");

                //check if stock availability is between 0 and 100
                Assert.That(enriched.stock_availability, Is.InRange(0, 100), $"Stock availability should be between 0 and 100 for product {enriched.id}");

                // Check if the popularity score is non-negative    
                Assert.That(enriched.popularity_score, Is.GreaterThanOrEqualTo(0), $"Popularity score should be non-negative for product {enriched.id}");
            }
        }

        [Test]
        public void Test_GroupedProducts()
        {

            Assert.That(groupedProducts.Count, Is.GreaterThan(0), "There should be at least one category.");

            foreach (var category in groupedProducts)
            {
                var productsInCategory = category.Value;
                // Ensure products in each category are sorted by price in descending order
                for (int i = 0; i < productsInCategory.Count - 1; i++)
                {
                    Assert.That(productsInCategory[i].original_price, Is.GreaterThanOrEqualTo(productsInCategory[i + 1].original_price), $"Products in {category.Key} should be sorted by price descending.");
                }
            }
        }

        [Test]
        public void Serialize_ThenDeserialize_GroupedProducts_ReturnsSameStructure()
        {
            // Act
            var serializedJson = JsonConvert.SerializeObject(groupedProducts, Formatting.Indented);
            var deserializedGroupedProducts = JsonConvert.DeserializeObject<Dictionary<string, List<EnrichedProduct>>>(serializedJson);

            // Assert
            Assert.That(deserializedGroupedProducts, Is.Not.Null, "Deserialized grouped products should not be null.");
            Assert.That(deserializedGroupedProducts!.Count, Is.EqualTo(groupedProducts.Count), "Deserialized grouped products should have the same number of categories.");

            foreach (var category in groupedProducts)
            {
                Assert.That(deserializedGroupedProducts.ContainsKey(category.Key), $"Deserialized grouped products should contain category {category.Key}.");
                Assert.That(deserializedGroupedProducts[category.Key].Count, Is.EqualTo(category.Value.Count), $"Deserialized products count for category {category.Key} should match the original.");
            }
        }

        [Test]
        public void Test_HttpGetRequest()
        {
            // Act
            var response = Program.HttpGet("https://fakestoreapi.com/products"); 
            Assert.That(response, Is.Not.Null, "API response should not be null.");

        }

    }

}