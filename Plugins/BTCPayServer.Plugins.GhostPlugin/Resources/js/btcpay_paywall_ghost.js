document.addEventListener("DOMContentLoaded", function () {
    console.log("Script Loaded");
    loadModalScript();

    if (localStorage.getItem('paywall_unlocked') === 'true') {
        console.log("User has unlocked content")
        unlockContent();
    }

    const paywallConfig = document.getElementById("paywall-config");

    document.addEventListener("click", function (event) {
        if (event.target.id === "payButton") {
            event.preventDefault();
            if (paywallConfig) {
                const price = paywallConfig.getAttribute("data-price");
                payButton.disabled = true;
                payButton.textContent = "Loading..."; 
                handleBitcoinPayment(price, payButton);
            }
            else {
                alert("Error: Payment configuration missing.");
            }
        }
    });
});

function handleBitcoinPayment(amount, button) {
    const url = BTCPAYSERVER_URL + "/plugins/" + BTCPAYSERVER_STORE_ID + "/ghost/api/paywall/create-invoice?amount=" + amount;
    fetch(url, {
        method: "GET",
        headers: { "Content-Type": "application/json" }
    })
        .then(response => response.json())
        .then(data => {
            console.log("Invoice created:", data);
            if (data.id) {
                showBTCPayModal(data);
            }
            else {
                button.disabled = false;
                button.textContent = "Unlock with Bitcoin";
                alert("Error: Failed to generate invoice. Please contact admin");
            }
        })
        .catch(error => {
            button.disabled = false;
            button.textContent = "Unlock with Bitcoin";
            console.error("Payment initiation failed. Error:", error);
            alert("Error: Payment initiation failed. Please try again.");
        });
}



const showBTCPayModal = function (data) {
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
                        console.log('Invoice paid.');
                        unlockContent();
                        break;
                    case 'expired':
                        window.btcpay.hideFrame();
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
        } else {
            if (event.data === 'close') {
                if (invoice_paid === true) {
                    unlockContent();
                    console.log('Invoice paid.');
                }
                console.log('Modal closed.')
            }
        }
    });
    const isObject = obj => {
        return Object.prototype.toString.call(obj) === '[object Object]'
    }
};


function unlockContent() {
    document.getElementById('paywall-overlay').style.display = 'none';
    document.getElementById('paywall-content').style.display = 'block';

    localStorage.setItem('paywall_unlocked', 'true');
}



const loadModalScript = () => {
    const script = document.createElement('script');
    script.src = BTCPAYSERVER_URL + '/plugins/' + BTCPAYSERVER_STORE_ID + '/ghost/api/btcpay-ghost.js';
    document.head.appendChild(script);
    script.onload = function () {
        console.log('External modal script loaded successfully.');
    };
    script.onerror = function () {
        console.error('Error loading the external modal script.');
    };
}