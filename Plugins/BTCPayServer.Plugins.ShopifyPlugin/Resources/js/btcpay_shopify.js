
const handleBitcoinPayment = (event) => {
    event.preventDefault();
    clearInterval(pollInterval);

    const cartData = {
        storeId: BTCPAYSERVER_STORE_ID,
        cartId,
        currency,
        total: totalAmount,
        email
    };
    fetch(BTCPAYSERVER_URL + '/stores/' + BTCPAYSERVER_STORE_ID + '/plugins/bigcommerce/create-order', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(cartData)
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
                    case 'complete':
                    case 'paid':
                    case 'confirmed':
                        invoice_paid = true;
                        showOrderConfirmation(data.orderId, data.id);
                        console.log('Invoice paid.');
                        break;
                    case 'expired':
                        window.btcpay.hideFrame();
                        // todo: show error message
                        console.error('Invoice expired.');
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
