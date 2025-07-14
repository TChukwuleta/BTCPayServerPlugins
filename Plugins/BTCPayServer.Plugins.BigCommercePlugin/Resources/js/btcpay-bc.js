
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
                showBTCPayModal(data);
            }
        })
        .catch(error => {
            console.error('Payment initiation failed:', error);
        });
}

const getCart = () => {
    return fetch('/api/storefront/carts', { credentials: 'include' })
        .then(res => res.json())
        .then(carts => {
            if (!Array.isArray(carts) || !carts.length) {
                throw new Error('No cart found');
            }
            const cart = carts[0];
            return fetch(`/api/storefront/checkouts/${cart.id}`, { credentials: 'include' })
                .then(res => {
                    if (!res.ok) throw new Error(`Checkout fetch failed`);
                    return res.json();
                })
                .then(checkout => {
                    const result = {
                        id: cart.id,
                        currency: cart.currency.code,
                        amount: checkout.grandTotal,
                        customerEmail: cart.email
                    };
                    return result;
                });
        })
        .catch(error => {
            console.error('Error fetching full cart details:', error);
            throw error;
        });
};

// Show BTCPay modal.
const showBTCPayModal = function(data) {
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
                switch (event.data.status.toLowerCase()) {
                    case 'complete':
                    case 'paid':
                    case 'confirmed':
                    case 'processing':
                    case 'settled':
                        invoice_paid = true;
                        showOrderConfirmation(data.orderId, data.id);
                        console.log('Invoice paid.');
                        break;
                    case 'expired':
                        window.btcpay.hideFrame();
                        // todo: show error message
                        console.error('Invoice expired.');
                        break;
                    case 'invalid':
                        window.btcpay.hideFrame();
                        console.error('Invalid invoice.');
                        break;
                    default:
                        console.error('Unknown status: ' + event.data.status);
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
    script.src = BTCPAYSERVER_URL + '/stores/' + BTCPAYSERVER_STORE_ID + '/plugins/bigcommerce/modal/btcpay.js';
    document.head.appendChild(script);
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
