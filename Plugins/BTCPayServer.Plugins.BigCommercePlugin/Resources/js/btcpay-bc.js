
const observePaymentOptions = () => {
    const checkoutForm = document.querySelector('.checkout-form');
    if (!checkoutForm) return;

    const paymentButton = document.getElementById('checkout-payment-continue');
    if (!paymentButton) return;

    const updatePaymentButton = () => {
        const bitcoinOptionSelected = Array.from(checkoutForm.querySelectorAll('input[name="paymentProviderRadio"]')).some(radio => {
            return radio.nextElementSibling && radio.nextElementSibling.innerText.includes('Bitcoin') && radio.checked;
        });

        if (bitcoinOptionSelected) {
            paymentButton.textContent = 'Pay with Bitcoin';
            paymentButton.onclick = handleBitcoinPayment;
        } else {
            paymentButton.textContent = 'Place Order';
            paymentButton.onclick = null; // Reset to default behavior
        }
    };

    const paymentOptions = checkoutForm.querySelectorAll('input[name="paymentProviderRadio"]');
    paymentOptions.forEach(radio => {
        radio.addEventListener('change', updatePaymentButton);
    });

    // Initial call to set the correct button state
    updatePaymentButton();
}

const handleBitcoinPayment = (event) => {
    event.preventDefault();
    const checkoutForm = document.querySelector('.checkout-form');
    event.target.textContent = 'Processing ...';
    clearInterval(pollInterval);

    getCart()
        .then(cart => {
            return fetch(BTCPAYSERVER_URL + 'plugins/' + BTCPAYSERVER_STORE_ID + '/bigcommerce/create-order', {
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


// Function to get the cart data
const getCart = () => {
    // Return the fetch promise
    return fetch('/api/storefront/carts', {
        credentials: 'include'
    })
        .then(response => response.json())
        .then(myJson => {
            const cart = {
                id: myJson[0].id,
                currency: myJson[0].currency.code,
                amount: myJson[0].cartAmount,
                customerEmail: myJson[0].email
            };
            console.log('Log Cart');
            console.table(cart);
            // Resolve the promise with the cart data
            return cart;
        })
        .catch(error => {
            console.error('Error fetching cart:', error);
            // You might want to handle the rejected state by returning a default value or re-throwing the error
            throw error;
        });
}

// Show BTCPay modal.
const showBTCPayModal = function(data, checkoutForm) {
    console.log('Triggered showBTCPayModal()');

    if (data.id == undefined) {
        console.error('No invoice id provided, aborting.');
    }
    window.btcpay.setApiUrlPrefix(BTCPAYSERVER_URL);
    window.btcpay.showInvoice(data.id);

    let invoice_paid = false;
    window.btcpay.onModalReceiveMessage(function (event) {
        if (isObject(event.data)) {
            if (event.data.status) {
                switch (event.data.status) {
                     'complete':
                     'paid':
                        invoice_paid = true;
                        showOrderConfirmation(data.orderId, data.id);
                        console.log('Invoice paid.');
                        break;
                     'expired':
                        window.btcpay.hideFrame();
                        // todo: show error message
                        console.error('Invoice expired.')
                        break;
                }
            }
        } else { // handle event.data "loaded" "closed"
            if (event.data === 'close') {
                if (invoice_paid === true) {
                    showOrderConfirmation(data.orderId, data.id);
                }
                console.log('Modal closed.')
            }
        }
    });
    const isObject = obj => {
        return Object.prototype.toString.call(obj) === '[object Object]'
    }
};

const showOrderConfirmation = (orderId, invoiceId) => {
    // Create an overlay element
    const overlay = document.createElement('div');
    overlay.id = 'overlay';
    overlay.style.backgroundColor = 'rgba(0, 0, 0, 0.7)';
    overlay.style.position = 'fixed';
    overlay.style.top = '0';
    overlay.style.left = '0';
    overlay.style.width = '100%';
    overlay.style.height = '100%';
    overlay.style.zIndex = '9999';

    // Create an inner div element
    const innerDiv = document.createElement('div');
    innerDiv.style.width = '500px';
    innerDiv.style.minHeight = '250px';
    innerDiv.style.backgroundColor = 'white';
    innerDiv.style.position = 'absolute';
    innerDiv.style.top = '50%';
    innerDiv.style.left = '50%';
    innerDiv.style.transform = 'translate(-50%, -50%)';
    innerDiv.style.padding = '35px';

    // Headline:
    const h3 = document.createElement('h3');
    h3.textContent = 'Order confirmed';

    // Message:
    const message = `Thank you!\n\nThe payment for your <strong>order ${orderId}</strong> was registered. As soon as the payment confirms you will get notified by us.\n\nFor future reference your payment invoice id is <em>${invoiceId}</em>.\n\n`;
    const p = document.createElement('p');
    p.innerHTML = message;

    // Create a link to return to the store.
    const redirectLink = document.createElement('a');
    redirectLink.href = '/';
    redirectLink.textContent = 'Return to store';

    // Append elements together
    innerDiv.appendChild(h3);
    innerDiv.appendChild(p);
    innerDiv.appendChild(redirectLink);

    overlay.appendChild(innerDiv);

    // Show the order confirmation message after 3 seconds.
    setTimeout(() => {
        // Append overlay to the body
        document.body.appendChild(overlay);
        window.btcpay.hideFrame();
    }, 2000);
}

const loadModalScript = () => {
    const script = document.createElement('script');
    script.src = BTCPAYSERVER_URL + /plugins/ + BTCPAYSERVER_STORE_ID + '/bigcommerce/modal/btcpay.js';
    document.head.appendChild(script);

    // Optional: Handle loading and error events
    script.onload = function() {
        console.log('External modal script loaded successfully.');
    };

    script.onerror = function() {
        console.error('Error loading the external modal script.');
    };
}


// Entrypoint.
loadModalScript();
const pollInterval = setInterval(observePaymentOptions, 300);
