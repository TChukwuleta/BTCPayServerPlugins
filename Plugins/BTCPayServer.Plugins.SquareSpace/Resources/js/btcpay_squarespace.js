
(function() {
  // Only run on checkout page
  // if (!window.location.pathname.includes("/checkout")) return;

    // Avoid double injection
    if (window.__btcpay_injected) return;
    window.__btcpay_injected = true;

  document.addEventListener("DOMContentLoaded", () => {
    // Locate the checkout confirm button
    const confirmBtn = document.querySelector('button[type="submit"]');

    if (!confirmBtn) return;

    // Create Pay with Bitcoin button
    const btcBtn = document.createElement("button");
    btcBtn.innerText = "Pay with Bitcoin";
    btcBtn.style.background = "#f7931a";
    btcBtn.style.color = "#fff";
    btcBtn.style.padding = "10px 20px";
    btcBtn.style.marginLeft = "10px";
    btcBtn.style.border = "none";
    btcBtn.style.borderRadius = "4px";
    btcBtn.style.cursor = "pointer";

    // Insert next to confirm button
    confirmBtn.parentNode.insertBefore(btcBtn, confirmBtn.nextSibling);

    // Click handler
    btcBtn.onclick = async (e) => {
        e.preventDefault();

    // Scrape cart details
    const cartItems = [];
      document.querySelectorAll('.sqs-cart-item').forEach(item => {
        const name = item.querySelector('.sqs-cart-item-title')?.innerText || 'Unknown';
    const qty = parseInt(item.querySelector('.sqs-cart-item-quantity')?.innerText || '1', 10);
    const price = parseFloat(item.querySelector('.sqs-cart-item-price')?.innerText.replace(/[^0-9.]/g, '') || '0');
    cartItems.push({name, qty, price});
      });

    const totalAmount = parseFloat(document.querySelector('.sqs-cart-total .sqs-money')?.innerText.replace(/[^0-9.]/g, '') || '0');

    // Send to backend to create BTCPay invoice
    try {
        const response = await fetch("https://yourserver.com/create-invoice", {
        method: "POST",
    headers: {"Content-Type": "application/json" },
    body: JSON.stringify({
        items: cartItems,
    total: totalAmount,
    currency: "USD",  // optional: dynamically detect
    checkoutUrl: window.location.href
          })
        });
    const data = await response.json();
    if (data.invoiceUrl) {
        window.location.href = data.invoiceUrl;
        } else {
        alert("Failed to create Bitcoin invoice.");
        }
      } catch (err) {
        console.error(err);
    alert("Error connecting to Bitcoin payment.");
      }
    };
  });
})();
