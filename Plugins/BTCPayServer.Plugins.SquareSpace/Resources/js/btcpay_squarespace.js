(function () {
    console.log("[BTCPay][Init] Script loaded");

    if (window.__btcpay_injected) return;
    window.__btcpay_injected = true;

    function injectButton() {
        const checkoutContainer = document.querySelector('.cart-checkout');
        if (!checkoutContainer) return;

        if (document.querySelector("#btcpay-bitcoin-btn")) return; 

        const btcBtn = document.createElement("button");
        btcBtn.type = "button";
        btcBtn.id = "btcpay-bitcoin-btn";

        const checkoutBtn = checkoutContainer.querySelector('a, button');
        if (checkoutBtn) {
            const styles = window.getComputedStyle(checkoutBtn);
            btcBtn.style.background = "#51B13E";
            btcBtn.style.color = "#FFFFFF";
            btcBtn.style.fontSize = styles.fontSize;
            btcBtn.style.fontWeight = styles.fontWeight;
            btcBtn.style.fontFamily = styles.fontFamily;
            btcBtn.style.padding = styles.padding;
            btcBtn.style.borderRadius = styles.borderRadius;
            btcBtn.style.border = styles.border;
            btcBtn.style.width = "100%";
            btcBtn.style.cursor = "pointer";
            btcBtn.style.textAlign = "center";
            btcBtn.style.marginBottom = "12px";
        }
        btcBtn.innerText = "Pay with BTCPay";

        checkoutContainer.prepend(btcBtn);

        btcBtn.addEventListener("click", async (e) => {
            e.preventDefault();
            btcBtn.innerText = "Loading...";
            btcBtn.disabled = true;

            console.log("[BTCPay][Click] BTCPay button clicked");

            const cartScript = document.querySelector('#sqs-cart-root > script[type="application/json"]');
            if (!cartScript) {
                alert("Unable to read cart data");
                btcBtn.innerText = "Pay with BTCPay";
                btcBtn.disabled = false;
                return;
            }

            const cartData = JSON.parse(cartScript.innerText).cart;
            console.log("[BTCPay][Cart]", cartData);

            // TODO: send cartData to backend to create BTCPay invoice
            // Example: fetch('/create-invoice', { method: 'POST', body: JSON.stringify(cartData) })

            // Reset button after request (for testing)
            setTimeout(() => {
                btcBtn.innerText = "Pay with BTCPay";
                btcBtn.disabled = false;
            }, 3000);
        });

        console.log("[BTCPay][UI] BTCPay button injected");
    }

    function waitForBody(callback) {
        if (document.body) {
            callback();
        } else {
            document.addEventListener("DOMContentLoaded", () => callback());
        }
    }

    waitForBody(() => {
        const observer = new MutationObserver(() => injectButton());
        observer.observe(document.body, { childList: true, subtree: true });

        injectButton();
    });
})();
