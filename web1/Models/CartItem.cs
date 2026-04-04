namespace web1.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public Product Product { get; set; } = new Product();
        public int Quantity { get; set; }
        public string? SelectedSize { get; set; }
        public string? SelectedColor { get; set; }
        
        public decimal TotalPrice => (Product.Price ?? 0) * Quantity;
    }
}
