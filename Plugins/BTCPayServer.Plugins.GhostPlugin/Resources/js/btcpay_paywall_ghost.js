/* btcpay_paywall_ghost */
document.addEventListener("DOMContentLoaded", function () {
    console.log("Script Loaded");

    document.addEventListener("click", function (event) {
        if (event.target.id === "payButton") {
            console.log("Hello world");
        }
    });
});


const handleBitcoinPayment = (event) => {
    event.preventDefault();
    const checkoutForm = document.querySelector('.checkout-form');
    event.target.textContent = 'Processing ...';
    clearInterval(pollInterval);

    getCart()
        .then(cart => {
            console.log(cart);
            return fetch(BTCPAYSERVER_URL + '/stores/' + BTCPAYSERVER_STORE_ID + '/plugins/bigcommerce/create-order', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    storeId: BTCPAYSERVER_STORE_ID,
                    cartId: cart.id,
                    currency: cart.currency,
                    total: cart.amount,
                    email: cart.customerEmail
                })
            });
        })
        .then(response => response.json())
        .then(data => {
            console.warn('Payment initiation successful:', data);
            if (data.id) {
                showBTCPayModal(data, checkoutForm);
            }
        })
        .catch(error => {
            console.error('Payment initiation failed:', error);
        });
}
