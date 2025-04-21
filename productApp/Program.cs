
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace productApp
{
    public class Program
    {
        static readonly HttpClient Client = new HttpClient();
        static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(); // Cache to store API responses
        static readonly Random RandomGenerator = new Random(); // Random generator for additional fields
        static async Task Main(string[] args)
        {
            try
            {
                var url = "https://fakestoreapi.com/products";

                double? minPrice = null;
                double? maxPrice = null;

                var response = await HttpGet(url);

                // Deserialize the JSON response into a list of products
                var products = JsonConvert.DeserializeObject<List<Product>>(response);

                if (products != null)
                {
                    var outputFormat = "json";  // Default output file format to JSON

                    ParseArguments(args, ref minPrice, ref maxPrice, ref outputFormat);

                    // Filter the products based on the price range if provided
                    products = FilterProductsByPriceRange(products, minPrice, maxPrice);

                    // Enrich the products with additional fields
                    var enrichedProducts = EnrichProducts(products);

                    // First Group products by category, then sort by price in descending order within that category 
                    var groupedProducts = GroupAndSortProducts(enrichedProducts);

                    switch (outputFormat)
                    {
                        case "json":
                            // Save the JSON output to a file
                            var outputJson = JsonConvert.SerializeObject(groupedProducts, Formatting.Indented);
                            File.WriteAllText("grouped_products.json", outputJson);
                            Console.WriteLine("JSON file saved as grouped_products.json");
                            break;

                        case "csv":
                            SaveToCsv(groupedProducts); // Save the grouped products to a CSV file
                            break;

                        case "xml":
                            // convert json to xml
                            SaveToXml(groupedProducts); // Save the grouped products to an XML file
                            break;

                        default:
                            Console.WriteLine("Unsupported format. Please use json, csv, or xml");
                            break;
                    }
                }
                else
                {
                    Console.WriteLine("Failed to deserialize JSON response into products.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        public static async Task<string> HttpGet(string url)
        {
            const int maxRetries = 3;
            const int delayMilliseconds = 2000;
            string logDirectory = "logs";
            string logFileName = $"api_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string logFilePath = Path.Combine(logDirectory, logFileName);

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Check if the response is already cached
                    if (Cache.ContainsKey(url))
                    {
                        string logMessage = $"[{DateTime.Now}] Returning cached response for URL: {url}";
                        File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                        Console.WriteLine(logMessage);
                        return Cache[url];
                    }

                    string result = string.Empty;
                    using (var request = new HttpRequestMessage())
                    {
                        request.RequestUri = new Uri(url);
                        request.Method = HttpMethod.Get;

                        // Log the API request
                        string requestLog = $"[{DateTime.Now}] Sending GET request to URL: {url}";
                        File.AppendAllText(logFilePath, requestLog + Environment.NewLine);
                        Console.WriteLine(requestLog);

                        var response = await Client.SendAsync(request);

                        // Log the response status
                        string responseLog = $"[{DateTime.Now}] Response received. Status Code: {response.StatusCode}";
                        File.AppendAllText(logFilePath, responseLog + Environment.NewLine);
                        Console.WriteLine(responseLog);

                        // Throw an exception if the response indicates failure
                        response.EnsureSuccessStatusCode();

                        result = await response.Content.ReadAsStringAsync();

                    }

                    // Cache the response
                    Cache[url] = result;
                    string cacheLog = $"[{DateTime.Now}] Response cached for URL: {url}";
                    File.AppendAllText(logFilePath, cacheLog + Environment.NewLine);
                    Console.WriteLine(cacheLog);
                    return result;
                }
                catch (Exception ex)
                {

                    string errorLog = $"[{DateTime.Now}] Attempt {attempt} failed for URL: {url}. Error: {ex.Message}";
                    File.AppendAllText(logFilePath, errorLog + Environment.NewLine);
                    Console.WriteLine(errorLog);

                    if (attempt == maxRetries)
                    {

                        string maxRetryLog = $"[{DateTime.Now}] Max retry attempts reached for URL: {url}. Failing the request.";
                        File.AppendAllText(logFilePath, maxRetryLog + Environment.NewLine);
                        Console.WriteLine(maxRetryLog);
                        throw;
                    }

                    // Wait before retrying
                    await Task.Delay(delayMilliseconds);
                }
            }


            throw new InvalidOperationException("Unexpected error in HttpGet.");
        }

        static void ParseArguments(string[] args, ref double? minPrice, ref double? maxPrice, ref string outputFormat)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("--minPrice="))
                {
                    if (double.TryParse(arg.Substring("--minPrice=".Length), out double parsedMinPrice))
                    {
                        minPrice = parsedMinPrice;
                        Console.WriteLine($"Minimum price set to: {minPrice}");
                    }
                    else
                    {
                        Console.WriteLine("Invalid value for --minPrice. Please provide a valid number.");
                        return;
                    }
                }
                else if (arg.StartsWith("--maxPrice="))
                {
                    if (double.TryParse(arg.Substring("--maxPrice=".Length), out double parsedMaxPrice))
                    {
                        maxPrice = parsedMaxPrice;
                        Console.WriteLine($"Maximum price set to: {maxPrice}");
                    }
                    else
                    {
                        Console.WriteLine("Invalid value for --maxPrice. Please provide a valid number.");
                        return;
                    }
                }
                else if (arg.StartsWith("--fileFormat="))
                {
                    var format = arg.Substring("--fileFormat=".Length).ToLower();
                    if (format == "json" || format == "csv" || format == "xml")
                    {
                        outputFormat = format;
                        Console.WriteLine($"File format set to: {outputFormat}");
                    }
                    else
                    {
                        Console.WriteLine("Invalid value for --fileFormat. Please use 'json', 'csv', or 'xml'.");
                        return;
                    }
                }
            }
        }

        public static List<Product> FilterProductsByPriceRange(List<Product> products, double? minPrice, double? maxPrice)
        {
            if (minPrice.HasValue)
            {
                products = products.Where(p => p.price >= minPrice.Value).ToList();
                Console.WriteLine($"Filtered products with price >= {minPrice}");
            }

            if (maxPrice.HasValue)
            {
                products = products.Where(p => p.price <= maxPrice.Value).ToList();
                Console.WriteLine($"Filtered products with price <= {maxPrice}");
            }

            return products;
        }
        public static List<EnrichedProduct> EnrichProducts(List<Product> products)
        {
            return products.Select(p => new EnrichedProduct
            {
                id = p.id,
                title = p.title,
                original_price = p.price,
                description = p.description,
                category = p.category,
                discounted_price = Math.Round(p.price * (1 - RandomGenerator.Next(5, 21) / 100.0), 2), // Apply random discount (5-20%)
                stock_availability = RandomGenerator.Next(0, 101), // Random stock levels between 0 and 100
                popularity_score = Math.Round(p.price * RandomGenerator.Next(0, 101) / 100.0, 2) // Popularity score based on price and stock
            }).ToList();
        }
        public static Dictionary<string, List<EnrichedProduct>> GroupAndSortProducts(List<EnrichedProduct> enrichedProducts)
        {
            // Group products by category and sort by price in descending order within that category
            return enrichedProducts
            .GroupBy((EnrichedProduct p) => p.category)
            .ToDictionary(
                g => g.Key!,
                g => g.OrderByDescending(p => p.original_price).ToList()
            );
        }
        public static void SaveToCsv(Dictionary<string, List<EnrichedProduct>> groupedProducts)
        {
            // Save the grouped products to a CSV file
            using (var writer = new StreamWriter("grouped_products.csv"))
            {
                writer.WriteLine("Category,ID,Title,Original Price,Discounted Price,Stock Availability,Popularity Score");
                foreach (var category in groupedProducts)
                {
                    foreach (var product in category.Value)
                    {
                        writer.WriteLine($"{category.Key},{product.id},{product.title},{product.original_price},{product.discounted_price},{product.stock_availability},{product.popularity_score}");
                    }
                }
            }
            Console.WriteLine("CSV file saved as grouped_products.csv");
        }

        public static void SaveToXml(Dictionary<string, List<EnrichedProduct>> groupedProducts)
        {
            // Save the grouped products to an XML file
            var xmlDoc = new XDocument(new XElement("Products"));
            if (xmlDoc.Root == null)
            {
                throw new InvalidOperationException("The XML document root is null.");
            }
            foreach (var category in groupedProducts)
            {
                var categoryElement = new XElement("Category", new XAttribute("Name", category.Key));
                foreach (var product in category.Value)
                {
                    var productElement = new XElement("Product",
                        new XElement("ID", product.id),
                        new XElement("Title", product.title),
                        new XElement("OriginalPrice", product.original_price),
                        new XElement("DiscountedPrice", product.discounted_price),
                        new XElement("StockAvailability", product.stock_availability),
                        new XElement("PopularityScore", product.popularity_score)
                    );
                    categoryElement.Add(productElement);
                }
                xmlDoc.Root.Add(categoryElement);
            }
            xmlDoc.Save("grouped_products.xml");
            Console.WriteLine("XML file saved as grouped_products.xml");
        }
    }

    public class Product
    {
        public int id { get; set; }
        public string? title { get; set; }
        public double price { get; set; }
        public string? description { get; set; }
        public string category { get; set; } = string.Empty;
        public string? image { get; set; }
        public Rating? rating { get; set; }
    }

    public class Rating
    {
        public double rate { get; set; }
        public int count { get; set; }
    }
    public class EnrichedProduct
    {
        public int id { get; set; }
        public string? title { get; set; }
        public double original_price { get; set; }
        public string? description { get; set; }
        public string? category { get; set; }
        public double discounted_price { get; set; }
        public int stock_availability { get; set; }
        public double popularity_score { get; set; }
    }
}